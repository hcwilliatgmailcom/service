using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oracle.ManagedDataAccess.Client;
using System.Runtime.CompilerServices;

namespace service.Data;

/// <summary>
/// Appends one row to CMDB.AUDITHISTORY for every property changed on any
/// tracked entity.  The table (and its indexes) are created on first use.
///
/// One row per changed property keeps the history easily queryable:
///   SELECT * FROM CMDB.AUDITHISTORY WHERE TABLENAME='CMDB.SERVERS' ORDER BY CHANGEDAT DESC
/// </summary>
public sealed class AuditHistoryInterceptor(
    IHttpContextAccessor http,
    ILogger<AuditHistoryInterceptor> logger) : SaveChangesInterceptor
{
    // ── Configuration ────────────────────────────────────────────────────────
    private const string AuditSchema = "CMDB";
    private const string AuditTable  = "AUDITHISTORY";
    private const int    MaxValueLen = 4000;          // VARCHAR2 column width

    // ── Table bootstrap (once per process) ──────────────────────────────────
    private static volatile bool _tableReady = false;
    private static readonly SemaphoreSlim _ddlLock = new(1, 1);

    // ── Per–SaveChanges state, keyed by DbContext instance ──────────────────
    // ConditionalWeakTable never prevents GC of the context key.
    private static readonly ConditionalWeakTable<DbContext, PendingBatch> _batches = new();

    // ── INSERT SQL (EF {n} positional placeholders become DB parameters) ─────
    private static readonly string InsertSql =
        $"INSERT INTO {AuditSchema}.{AuditTable} " +
        "(TABLENAME,ENTITYID,CHANGETYPE,PROPERTYNAME,OLDVALUE,NEWVALUE,CHANGEDBY,CHANGEDAT) " +
        "VALUES ({0},{1},{2},{3},{4},{5},{6},{7})";

    // ── DDL (PL/SQL – idempotent) ────────────────────────────────────────────
    // Single-quoted inner SQL avoids issues with interpolated strings inside EXECUTE IMMEDIATE.
    private static readonly string DdlSql = $"""
        DECLARE
          v NUMBER;
        BEGIN
          SELECT COUNT(*) INTO v FROM all_tables
          WHERE  owner = '{AuditSchema}' AND table_name = '{AuditTable}';
          IF v = 0 THEN
            EXECUTE IMMEDIATE 'CREATE TABLE {AuditSchema}.{AuditTable} (
              ID           NUMBER        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
              TABLENAME    VARCHAR2(200) NOT NULL,
              ENTITYID     VARCHAR2(500),
              CHANGETYPE   VARCHAR2(10)  NOT NULL,
              PROPERTYNAME VARCHAR2(200),
              OLDVALUE     VARCHAR2(4000),
              NEWVALUE     VARCHAR2(4000),
              CHANGEDBY    VARCHAR2(200),
              CHANGEDAT    TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL
            )';
            EXECUTE IMMEDIATE 'CREATE INDEX IDXAHTBL  ON {AuditSchema}.{AuditTable} (TABLENAME)';
            EXECUTE IMMEDIATE 'CREATE INDEX IDXAHENT  ON {AuditSchema}.{AuditTable} (TABLENAME, ENTITYID)';
            EXECUTE IMMEDIATE 'CREATE INDEX IDXAHWHEN ON {AuditSchema}.{AuditTable} (CHANGEDAT)';
          END IF;
        END;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // Sync overrides
    // ════════════════════════════════════════════════════════════════════════

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData e, InterceptionResult<int> result)
    {
        if (e.Context is { } ctx)
        {
            EnsureTableSync(ctx);
            Collect(ctx);
        }
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData e, int result)
    {
        if (e.Context is { } ctx)
            FlushSync(ctx);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData e)
    {
        if (e.Context is { } ctx) _batches.Remove(ctx);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Async overrides
    // ════════════════════════════════════════════════════════════════════════

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (e.Context is { } ctx)
        {
            await EnsureTableAsync(ctx, ct);
            Collect(ctx);
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    {
        if (e.Context is { } ctx)
            await FlushAsync(ctx, ct);
        return result;
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData e, CancellationToken ct = default)
    {
        if (e.Context is { } ctx) _batches.Remove(ctx);
        return Task.CompletedTask;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Change collection  (called inside SavingChanges, before DB round-trip)
    // ════════════════════════════════════════════════════════════════════════

    private static void Collect(DbContext ctx)
    {
        var batch = new PendingBatch();

        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            // Skip keyless entities (views, query types) – they can't be audited meaningfully.
            if (entry.Metadata.FindPrimaryKey() is null)
                continue;

            var rawTable  = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
            var schema    = entry.Metadata.GetSchema() ?? AuditSchema;
            var tableName = $"{schema}.{rawTable}";

            var changeType = entry.State switch
            {
                EntityState.Added    => "INSERT",
                EntityState.Modified => "UPDATE",
                _                    => "DELETE"
            };

            // For Added entities the PK is a temp value until after SaveChanges.
            // We store a null EntityId now and resolve it in Flush after the DB
            // has populated the real identity value.
            var entityId  = entry.State == EntityState.Added ? null : PkString(entry);
            List<AuditRow>? addedRows = null;

            foreach (var prop in entry.Properties)
            {
                // For updates, only log actually-changed properties.
                if (entry.State == EntityState.Modified && !prop.IsModified)
                    continue;

                var oldValue = entry.State is EntityState.Modified or EntityState.Deleted
                                   ? Clip(prop.OriginalValue) : null;
                var newValue = entry.State is EntityState.Added or EntityState.Modified
                                   ? Clip(prop.CurrentValue) : null;

                // Skip if nothing actually changed (happens when _context.Update marks all props modified).
                if (entry.State == EntityState.Modified && oldValue == newValue)
                    continue;

                var row = new AuditRow
                {
                    TableName    = tableName,
                    ChangeType   = changeType,
                    EntityId     = entityId,
                    PropertyName = prop.Metadata.GetColumnName(),
                    OldValue     = oldValue,
                    NewValue     = newValue
                };

                batch.Entries.Add(row);

                if (entry.State == EntityState.Added)
                {
                    addedRows ??= [];
                    addedRows.Add(row);
                }
            }

            if (addedRows is not null)
                batch.AddedMap[entry.Entity] = addedRows;
        }

        if (batch.Entries.Count > 0)
            _batches.AddOrUpdate(ctx, batch);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Flush  (called inside SavedChanges, after the DB commit)
    // ════════════════════════════════════════════════════════════════════════

    private void FlushSync(DbContext ctx)
    {
        if (!_batches.TryGetValue(ctx, out var batch)) return;
        _batches.Remove(ctx);
        ResolveAddedIds(ctx, batch);

        var by = CurrentUser();
        var at = DateTime.UtcNow;
        try
        {
            foreach (var row in batch.Entries)
                ctx.Database.ExecuteSqlRaw(InsertSql,
                    row.TableName, row.EntityId, row.ChangeType,
                    row.PropertyName, row.OldValue, row.NewValue, by, at);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AuditHistory: failed to write audit rows (sync).");
        }
    }

    private async Task FlushAsync(DbContext ctx, CancellationToken ct)
    {
        if (!_batches.TryGetValue(ctx, out var batch)) return;
        _batches.Remove(ctx);
        ResolveAddedIds(ctx, batch);

        var by = CurrentUser();
        var at = DateTime.UtcNow;
        try
        {
            foreach (var row in batch.Entries)
                await ctx.Database.ExecuteSqlRawAsync(InsertSql,
                    new object[] { row.TableName!, row.EntityId!, row.ChangeType!,
                                   row.PropertyName!, row.OldValue!, row.NewValue!, by, at },
                    ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AuditHistory: failed to write audit rows (async).");
        }
    }

    // After SaveChanges, Added entities are Unchanged and their PK is populated.
    private static void ResolveAddedIds(DbContext ctx, PendingBatch batch)
    {
        if (batch.AddedMap.Count == 0) return;

        foreach (var tracked in ctx.ChangeTracker.Entries())
        {
            if (!batch.AddedMap.TryGetValue(tracked.Entity, out var rows)) continue;
            var id = PkString(tracked);
            foreach (var r in rows) r.EntityId = id;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Table bootstrap
    // ════════════════════════════════════════════════════════════════════════

    private static void EnsureTableSync(DbContext ctx)
    {
        if (_tableReady) return;
        _ddlLock.Wait();
        try
        {
            if (_tableReady) return;
            using var conn = new OracleConnection(ctx.Database.GetConnectionString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = DdlSql;
            cmd.ExecuteNonQuery();
            _tableReady = true;
        }
        finally { _ddlLock.Release(); }
    }

    private static async Task EnsureTableAsync(DbContext ctx, CancellationToken ct)
    {
        if (_tableReady) return;
        await _ddlLock.WaitAsync(ct);
        try
        {
            if (_tableReady) return;
            using var conn = new OracleConnection(ctx.Database.GetConnectionString());
            await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = DdlSql;
            await cmd.ExecuteNonQueryAsync(ct);
            _tableReady = true;
        }
        finally { _ddlLock.Release(); }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Utilities
    // ════════════════════════════════════════════════════════════════════════

    private string CurrentUser() =>
        http.HttpContext?.User?.Identity?.Name ?? "System";

    private static string? PkString(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        var pk = e.Metadata.FindPrimaryKey();
        if (pk is null) return null;
        return string.Join(",", pk.Properties.Select(p => e.Property(p.Name).CurrentValue?.ToString()));
    }

    private static string? Clip(object? v)
    {
        if (v is null or DBNull) return null;
        var s = Convert.ToString(v);
        return s is { Length: > MaxValueLen } ? s[..MaxValueLen] : s;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Inner types
    // ════════════════════════════════════════════════════════════════════════

    private sealed class AuditRow
    {
        public string? TableName    { get; init; }
        public string? ChangeType   { get; init; }
        public string? EntityId     { get; set; }   // set after save for Added
        public string? PropertyName { get; init; }
        public string? OldValue     { get; init; }
        public string? NewValue     { get; init; }
    }

    private sealed class PendingBatch
    {
        public List<AuditRow> Entries { get; } = [];

        // entity object → rows that need their EntityId filled in after save
        public Dictionary<object, List<AuditRow>> AddedMap { get; }
            = new(ReferenceEqualityComparer.Instance);
    }
}
