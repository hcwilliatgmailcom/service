using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace Cmdb.Services;

public class SchemaService
{
    private readonly string _connStr;
    private static Dictionary<string, EntityMeta>? _cache;
    private static readonly object _lock = new();

    public SchemaService(string connStr)
    {
        _connStr = connStr;
    }

    public OracleConnection GetConnection()
    {
        var builder = new OracleConnectionStringBuilder(_connStr)
        {
            StatementCacheSize = 0
        };
        var conn = new OracleConnection(builder.ToString());
        conn.Open();
        return conn;
    }

    public void ClearCache()
    {
        lock (_lock) { _cache = null; }
    }

    public Dictionary<string, EntityMeta> DiscoverEntities()
    {
        lock (_lock)
        {
            if (_cache != null) return _cache;
        }

        var entities = new Dictionary<string, EntityMeta>(StringComparer.OrdinalIgnoreCase);
        using var conn = GetConnection();

        var tables = Query(conn, @"
            SELECT t.TABLE_NAME, c.COMMENTS, 'TABLE' AS OBJECT_TYPE
            FROM USER_TABLES t
            LEFT JOIN USER_TAB_COMMENTS c ON c.TABLE_NAME = t.TABLE_NAME
            UNION ALL
            SELECT v.VIEW_NAME, c.COMMENTS, 'VIEW' AS OBJECT_TYPE
            FROM USER_VIEWS v
            LEFT JOIN USER_TAB_COMMENTS c ON c.TABLE_NAME = v.VIEW_NAME
            ORDER BY TABLE_NAME");

        foreach (var tbl in tables)
        {
            var tableName = tbl["TABLE_NAME"]?.ToString() ?? "";
            var comment = tbl["COMMENTS"]?.ToString() ?? "";
            var isView = tbl["OBJECT_TYPE"]?.ToString() == "VIEW";
            string icon = isView ? "bi-eye" : "bi-table", desc = tableName;

            if (!string.IsNullOrEmpty(comment) && comment.Contains('|'))
            {
                var parts = comment.Split('|', 2);
                icon = parts[0].Trim();
                desc = parts[1].Trim();
            }

            var meta = new EntityMeta
            {
                TableName = tableName,
                Icon = icon,
                Description = desc,
                DisplayName = SplitPascal(tableName),
                IsView = isView,
            };

            meta.Columns = GetColumns(conn, tableName);
            meta.PkColumns = GetPkColumns(conn, tableName);

            // Views have no PK constraints — use ID column as pseudo-PK if available
            if (isView && meta.PkColumns.Count == 0)
            {
                var idCol = meta.Columns.FirstOrDefault(c =>
                    c.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase));
                if (idCol != null)
                    meta.PkColumns.Add(idCol.ColumnName);
            }

            foreach (var col in meta.Columns)
            {
                if (col.ColumnName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase)
                    && !meta.PkColumns.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    var refTable = col.ColumnName[..^3];
                    col.IsFk = true;
                    col.FkRefTable = refTable;
                    col.FkRefPk = "ID";
                    col.FkDisplayCol = GetDisplayColumn(conn, refTable);
                    col.FkNavName = SplitPascal(refTable);
                }
            }

            if (meta.PkColumns.Count > 1)
                meta.IsCompositePk = true;

            meta.DisplayColumn = GetDisplayColumn(conn, tableName);

            entities[tableName] = meta;
        }

        lock (_lock) { _cache = entities; }
        return entities;
    }

    public List<ColumnMeta> GetColumns(OracleConnection conn, string tableName)
    {
        var cols = new List<ColumnMeta>();
        var rows = Query(conn,
            "SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, DATA_PRECISION, DATA_SCALE, NULLABLE, DATA_DEFAULT, COLUMN_ID FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :t ORDER BY COLUMN_ID",
            new OracleParameter("t", tableName.ToUpper()));

        foreach (var r in rows)
        {
            var colName = r["COLUMN_NAME"]?.ToString() ?? "";
            var dataType = r["DATA_TYPE"]?.ToString() ?? "";
            var nullable = r["NULLABLE"]?.ToString() == "Y";
            var dataDefault = r["DATA_DEFAULT"]?.ToString()?.Trim() ?? "";
            bool isIdentity = dataDefault.Contains("ISEQ$$") || dataDefault.Contains("GENERATED");

            cols.Add(new ColumnMeta
            {
                ColumnName = colName,
                DataType = dataType,
                IsNullable = nullable,
                IsIdentity = isIdentity,
            });
        }

        try
        {
            var identRows = Query(conn,
                "SELECT COLUMN_NAME FROM USER_TAB_IDENTITY_COLS WHERE TABLE_NAME = :t",
                new OracleParameter("t", tableName.ToUpper()));
            foreach (var ir in identRows)
            {
                var idCol = ir["COLUMN_NAME"]?.ToString() ?? "";
                var col = cols.FirstOrDefault(c => c.ColumnName.Equals(idCol, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.IsIdentity = true;
            }
        }
        catch { }

        return cols;
    }

    public List<string> GetPkColumns(OracleConnection conn, string tableName)
    {
        var pks = new List<string>();
        var rows = Query(conn, @"
            SELECT cc.COLUMN_NAME
            FROM USER_CONSTRAINTS c
            JOIN USER_CONS_COLUMNS cc ON cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
            WHERE c.TABLE_NAME = :t AND c.CONSTRAINT_TYPE = 'P'
            ORDER BY cc.POSITION",
            new OracleParameter("t", tableName.ToUpper()));

        foreach (var r in rows)
            pks.Add(r["COLUMN_NAME"]?.ToString() ?? "");

        return pks;
    }

    public string GetDisplayColumn(OracleConnection conn, string tableName)
    {
        var cols = Query(conn,
            "SELECT COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :t ORDER BY COLUMN_ID",
            new OracleParameter("t", tableName.ToUpper()));

        var nameCol = cols.FirstOrDefault(c => c["COLUMN_NAME"]?.ToString()?.Equals("NAME", StringComparison.OrdinalIgnoreCase) == true);
        if (nameCol != null) return nameCol["COLUMN_NAME"]?.ToString() ?? "NAME";

        var varcharCol = cols.FirstOrDefault(c => c["DATA_TYPE"]?.ToString()?.Contains("CHAR") == true);
        if (varcharCol != null) return varcharCol["COLUMN_NAME"]?.ToString() ?? "";

        return cols.FirstOrDefault()?["COLUMN_NAME"]?.ToString() ?? "ID";
    }

    public List<Dictionary<string, object?>> Query(OracleConnection conn, string sql, params OracleParameter[] parms)
    {
        try
        {
            return RunQuery(conn, sql, parms);
        }
        catch (OracleException ex) when (ex.Number == 24449)
        {
            conn.PurgeStatementCache();
            return RunQuery(conn, sql, parms);
        }
        catch (OracleException ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    private static List<Dictionary<string, object?>> RunQuery(OracleConnection conn, string sql, OracleParameter[] parms)
    {
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        foreach (var p in parms) cmd.Parameters.Add(new OracleParameter(p.ParameterName, p.Value));
        using var rdr = cmd.ExecuteReader();
        var results = new List<Dictionary<string, object?>>();
        while (rdr.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rdr.FieldCount; i++)
                row[rdr.GetName(i)] = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
            results.Add(row);
        }
        return results;
    }

    public Dictionary<string, object?>? QueryOne(OracleConnection conn, string sql, params OracleParameter[] parms)
    {
        var rows = Query(conn, sql, parms);
        return rows.Count > 0 ? rows[0] : null;
    }

    public int Execute(OracleConnection conn, string sql, params OracleParameter[] parms)
    {
        try
        {
            return RunExecute(conn, sql, parms);
        }
        catch (OracleException ex) when (ex.Number == 24449)
        {
            conn.PurgeStatementCache();
            return RunExecute(conn, sql, parms);
        }
        catch (OracleException ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    private static int RunExecute(OracleConnection conn, string sql, OracleParameter[] parms)
    {
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        foreach (var p in parms) cmd.Parameters.Add(new OracleParameter(p.ParameterName, p.Value));
        return cmd.ExecuteNonQuery();
    }

    public object? ExecuteScalar(OracleConnection conn, string sql, params OracleParameter[] parms)
    {
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        foreach (var p in parms) cmd.Parameters.Add(p);
        return cmd.ExecuteScalar();
    }

    public static string SplitPascal(string name)
    {
        name = name.Replace("_", " ");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", " ");
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
    }
}

public class EntityMeta
{
    public string TableName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Icon { get; set; } = "bi-table";
    public string Description { get; set; } = "";
    public List<ColumnMeta> Columns { get; set; } = new();
    public List<string> PkColumns { get; set; } = new();
    public string DisplayColumn { get; set; } = "NAME";
    public bool IsCompositePk { get; set; }
    public bool IsView { get; set; }
}

public class ColumnMeta
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsFk { get; set; }
    public string? FkRefTable { get; set; }
    public string? FkRefPk { get; set; }
    public string? FkDisplayCol { get; set; }
    public string? FkNavName { get; set; }
}
