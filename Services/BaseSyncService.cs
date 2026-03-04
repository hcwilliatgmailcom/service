using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using service.Data;

namespace service.Services;

/// <summary>
/// Shared infrastructure for REST-API sync services:
/// context creation, chunking, and a generic upsert loop.
/// </summary>
public abstract class BaseSyncService(IServiceProvider services)
{
    protected const int BatchSize = 500;

    // ── Context factory ──────────────────────────────────────────────────────

    protected CmdbContext CreateDbContext()
    {
        var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmdbContext>();
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        return db;
    }

    // ── Chunking ─────────────────────────────────────────────────────────────

    protected static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }

    // ── Generic upsert ───────────────────────────────────────────────────────

    /// <summary>
    /// Splits <paramref name="data"/> into inserts and updates using natural keys,
    /// then applies them in batches.
    /// </summary>
    /// <typeparam name="TEntity">EF entity type.</typeparam>
    /// <typeparam name="TDto">Source DTO type.</typeparam>
    /// <typeparam name="TKey">Natural / business key type (must support equality).</typeparam>
    /// <param name="data">All DTOs to process.</param>
    /// <param name="loadKeys">Returns the set of keys that already exist in the DB.</param>
    /// <param name="keyOf">Extracts the natural key from a DTO.</param>
    /// <param name="create">Creates a new entity from a DTO.</param>
    /// <param name="fetch">
    ///   Given a fresh DB context and a batch of keys, returns a dictionary of
    ///   existing entities keyed by their natural key (for the update path).
    /// </param>
    /// <param name="apply">Copies changed fields from a DTO onto an existing entity.</param>
    protected async Task<(int Added, int Updated)> UpsertAsync<TEntity, TDto, TKey>(
        IEnumerable<TDto> data,
        Func<CmdbContext, CancellationToken, Task<HashSet<TKey>>> loadKeys,
        Func<TDto, TKey> keyOf,
        Func<TDto, TEntity> create,
        Func<CmdbContext, IReadOnlyCollection<TKey>, CancellationToken, Task<Dictionary<TKey, TEntity>>> fetch,
        Action<TEntity, TDto> apply,
        CancellationToken ct) where TEntity : class where TKey : notnull
    {
        HashSet<TKey> existing;
        using (var db = CreateDbContext())
            existing = await loadKeys(db, ct);

        var list = data.ToList();
        var toInsert = list.Where(d => !existing.Contains(keyOf(d))).ToList();
        var toUpdate = list.Where(d =>  existing.Contains(keyOf(d))).ToList();
        int added = 0, updated = 0;

        foreach (var batch in Chunk(toInsert, BatchSize))
        {
            using var db = CreateDbContext();
            foreach (var dto in batch)
                db.Set<TEntity>().Add(create(dto));
            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
            added += batch.Count;
        }

        foreach (var batch in Chunk(toUpdate, BatchSize))
        {
            using var db = CreateDbContext();
            var keys = batch.Select(keyOf).ToList();
            var entities = await fetch(db, keys, ct);
            foreach (var dto in batch)
            {
                if (entities.TryGetValue(keyOf(dto), out var entity))
                {
                    apply(entity, dto);
                    updated++;
                }
            }
            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
        }

        return (added, updated);
    }
}
