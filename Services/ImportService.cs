using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using Service.Models;
using Service.Exceptions;
using static Service.Helpers.OracleHelpers;
using static Service.Helpers.JsonHelpers;

namespace Service.Services;

public class ImportService
{
    private readonly OracleConnection _conn;
    private readonly MetadataService _metadata;

    public ImportService(OracleConnection conn, MetadataService metadata)
    {
        _conn = conn;
        _metadata = metadata;
    }

    public object GetImportDrivers()
    {
        return new
        {
            drivers = new[]
            {
                new { driver = "oracle", label = "Oracle", defaultPort = 1521 }
            }
        };
    }

    public object TestExternalConnection(Dictionary<string, JsonElement> body)
    {
        using var extConn = ConnectExternal(body);
        var ver = "";
        try
        {
            using var cmd = new OracleCommand("SELECT BANNER FROM V$VERSION FETCH FIRST 1 ROW ONLY", extConn);
            ver = cmd.ExecuteScalar()?.ToString() ?? "";
        }
        catch { /* ignore */ }

        return new { ok = true, version = ver, driver = GetJsonString(body, "type") };
    }

    public object GetExternalTables(Dictionary<string, JsonElement> body)
    {
        using var extConn = ConnectExternal(body);
        var tables = new List<object>();

        using var cmd = new OracleCommand(
            "SELECT TABLE_NAME FROM USER_TABLES ORDER BY TABLE_NAME", extConn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            tables.Add(new { name = r.GetString(0) });

        return new { tables };
    }

    public object GetExternalColumns(Dictionary<string, JsonElement> body)
    {
        using var extConn = ConnectExternal(body);
        var table = GetJsonString(body, "table");
        var cols = FetchExtCols(extConn, table);
        return new { table, columns = cols };
    }

    public object PreviewExternalData(Dictionary<string, JsonElement> body)
    {
        using var extConn = ConnectExternal(body);
        var table = GetJsonString(body, "table");
        var limit = Math.Min(GetJsonInt(body, "limit", 20), 100);

        var rows = new List<Dictionary<string, object?>>();
        using var cmd = new OracleCommand(
            $"SELECT * FROM {Q(table)} FETCH FIRST {limit} ROWS ONLY", extConn);
        using var r = cmd.ExecuteReader();
        while (r.Read()) rows.Add(ReadRow(r));

        return new { table, count = rows.Count, rows };
    }

    public object SyncData(Dictionary<string, JsonElement> body)
    {
        var srcTable = GetJsonString(body, "source_table");
        var dstTable = GetJsonString(body, "target_table");
        var mode = GetJsonStringOrNull(body, "mode") ?? "merge";

        if (string.IsNullOrEmpty(srcTable) || string.IsNullOrEmpty(dstTable))
            throw new HttpException(400, "source_table und target_table erforderlich");

        var localTable = _metadata.ResolveTable(dstTable);
        var localCols = _metadata.GetColumns(localTable).Select(c => c.Name).ToList();
        var pk = _metadata.GetPrimaryKey(localTable);

        var srcCols = new List<string>();
        var dstCols = new List<string>();

        if (body.TryGetValue("mapping", out var mappingEl) && mappingEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in mappingEl.EnumerateArray())
            {
                var src = GetJsonStringOrNull(m, "src");
                var dst = GetJsonStringOrNull(m, "dst");
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst) && localCols.Contains(dst))
                {
                    srcCols.Add(src);
                    dstCols.Add(dst);
                }
            }
        }

        if (srcCols.Count == 0)
        {
            using var extConn2 = ConnectExternal(body);
            var extCols = FetchExtCols(extConn2, srcTable);
            foreach (var ec in extCols)
            {
                var match = localCols.FirstOrDefault(lc =>
                    lc.Equals(ec.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    srcCols.Add(ec.Name);
                    dstCols.Add(match);
                }
            }
        }

        if (srcCols.Count == 0)
            throw new HttpException(400, "Keine passenden Spalten. Bitte Mapping angeben.");

        List<Dictionary<string, object?>> sourceRows;
        using (var extConn = ConnectExternal(body))
        {
            var selectSql = $"SELECT {string.Join(",", srcCols.Select(Q))} FROM {Q(srcTable)}";
            sourceRows = new List<Dictionary<string, object?>>();
            using var cmd = new OracleCommand(selectSql, extConn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) sourceRows.Add(ReadRow(r));
        }

        if (sourceRows.Count == 0)
            return new { message = "Quelltabelle leer", source_rows = 0, inserted = 0, updated = 0, unchanged = 0, errors = new List<string>() };

        var localTbl = Q(localTable);
        var ins = 0;
        var upd = 0;
        var errors = new List<string>();

        if (mode == "replace")
        {
            using var del = new OracleCommand($"DELETE FROM {localTbl}", _conn);
            del.ExecuteNonQuery();
        }

        foreach (var (row, idx) in sourceRows.Select((r, i) => (r, i)))
        {
            try
            {
                var vals = srcCols.Select(sc => row.TryGetValue(sc, out var v) ? v : null).ToArray();

                if (mode == "merge")
                {
                    var usingCols = string.Join(", ", dstCols.Select((dc, j) => $":p{j} AS {Q(dc)}"));
                    var onClause = $"t.{Q(pk)} = s.{Q(pk)}";
                    var updateSet = string.Join(", ", dstCols.Where(dc => dc != pk).Select(dc => $"t.{Q(dc)} = s.{Q(dc)}"));
                    var insertCols = string.Join(", ", dstCols.Select(Q));
                    var insertVals = string.Join(", ", dstCols.Select(dc => $"s.{Q(dc)}"));

                    var mergeSql = $"MERGE INTO {localTbl} t USING (SELECT {usingCols} FROM DUAL) s ON ({onClause}) ";
                    if (!string.IsNullOrEmpty(updateSet))
                        mergeSql += $"WHEN MATCHED THEN UPDATE SET {updateSet} ";
                    mergeSql += $"WHEN NOT MATCHED THEN INSERT ({insertCols}) VALUES ({insertVals})";

                    using var cmd = new OracleCommand(mergeSql, _conn);
                    for (var j = 0; j < vals.Length; j++)
                        cmd.Parameters.Add(new OracleParameter($"p{j}", vals[j] ?? DBNull.Value));

                    var affected = cmd.ExecuteNonQuery();
                    if (affected > 0) ins++;
                }
                else
                {
                    var colList = string.Join(",", dstCols.Select(Q));
                    var phList = string.Join(",", dstCols.Select((_, j) => $":p{j}"));
                    var insertSql = $"INSERT INTO {localTbl} ({colList}) VALUES ({phList})";

                    using var cmd = new OracleCommand(insertSql, _conn);
                    for (var j = 0; j < vals.Length; j++)
                        cmd.Parameters.Add(new OracleParameter($"p{j}", vals[j] ?? DBNull.Value));

                    cmd.ExecuteNonQuery();
                    ins++;
                }
            }
            catch (Exception e)
            {
                errors.Add($"Zeile {idx + 1}: {e.Message}");
                if (errors.Count > 100) { errors.Add("..."); break; }
            }
        }

        return new
        {
            message = "Sync abgeschlossen",
            source_rows = sourceRows.Count,
            inserted = ins,
            updated = upd,
            unchanged = sourceRows.Count - ins - upd - errors.Count,
            errors,
            mode
        };
    }

    private OracleConnection ConnectExternal(Dictionary<string, JsonElement> body)
    {
        var type = GetJsonString(body, "type").ToLower();
        if (type != "oracle")
            throw new HttpException(400, $"Nur Oracle-Verbindungen werden unterstuetzt. Typ: '{type}'");

        var host = GetJsonStringOrNull(body, "host") ?? "localhost";
        var port = GetJsonInt(body, "port", 1521);
        var database = GetJsonString(body, "database");
        var user = GetJsonStringOrNull(body, "user") ?? "";
        var password = GetJsonStringOrNull(body, "password") ?? "";

        var connStr = $"Data Source={host}:{port}/{database};User Id={user};Password={password};";

        try
        {
            var conn = new OracleConnection(connStr);
            conn.Open();
            return conn;
        }
        catch (Exception e)
        {
            throw new HttpException(400, $"Verbindung fehlgeschlagen: {e.Message}");
        }
    }

    private List<ColumnInfo> FetchExtCols(OracleConnection conn, string table)
    {
        var cols = new List<ColumnInfo>();
        using var cmd = new OracleCommand(
            "SELECT COLUMN_NAME, DATA_TYPE, NULLABLE FROM USER_TAB_COLUMNS " +
            "WHERE TABLE_NAME = :tbl ORDER BY COLUMN_ID", conn);
        cmd.Parameters.Add(new OracleParameter("tbl", table));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            cols.Add(new ColumnInfo
            {
                Name = r.GetString(0),
                Type = r.GetString(1).ToLower(),
                Nullable = r.GetString(2) == "Y"
            });
        }
        return cols;
    }
}
