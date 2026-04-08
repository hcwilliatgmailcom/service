using Oracle.ManagedDataAccess.Client;
using Service.Models;
using Service.Exceptions;
using static Service.Helpers.OracleHelpers;

namespace Service.Services;

public class MetadataService
{
    private readonly ServiceConfig _config;
    private readonly OracleConnection _conn;
    private readonly Dictionary<string, List<ColumnInfo>> _colCache = new();
    private readonly Dictionary<string, string> _pkCache = new();
    private readonly Dictionary<string, bool> _viewCache = new();
    private readonly Dictionary<string, List<ForeignKeyInfo>> _fkCache = new();
    private readonly Dictionary<string, HashSet<string>> _identityCache = new();

    public MetadataService(OracleConnection conn, ServiceConfig config)
    {
        _conn = conn;
        _config = config;
    }

    public List<ResourceInfo> ListResources()
    {
        var result = new List<ResourceInfo>();
        using (var cmd = new OracleCommand(
            "SELECT TABLE_NAME, 'TABLE' AS OBJ_TYPE FROM USER_TABLES " +
            "UNION ALL " +
            "SELECT VIEW_NAME, 'VIEW' AS OBJ_TYPE FROM USER_VIEWS " +
            "ORDER BY 1", _conn))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var name = r.GetString(0);
                if (IsObjectEnabled(name))
                    result.Add(new ResourceInfo { Name = name, Type = r.GetString(1) });
            }
        }
        return result;
    }

    public List<ColumnInfo> GetColumns(string table)
    {
        if (_colCache.TryGetValue(table, out var cached)) return cached;

        var identityCols = GetIdentityColumns(table);
        var pk = GetPrimaryKey(table);
        var cols = new List<ColumnInfo>();

        using var cmd = new OracleCommand(
            "SELECT COLUMN_NAME, DATA_TYPE, NULLABLE, DATA_DEFAULT " +
            "FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :tbl ORDER BY COLUMN_ID", _conn);
        cmd.Parameters.Add(new OracleParameter("tbl", table));
        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            var name = r.GetString(0);
            cols.Add(new ColumnInfo
            {
                Name = name,
                Type = r.GetString(1).ToLower(),
                Nullable = r.GetString(2) == "Y",
                Pk = name == pk,
                Auto = identityCols.Contains(name),
                Default = r.IsDBNull(3) ? null : r.GetString(3).Trim()
            });
        }

        _colCache[table] = cols;
        return cols;
    }

    public string GetPrimaryKey(string table)
    {
        if (_pkCache.TryGetValue(table, out var cached)) return cached;

        using var cmd = new OracleCommand(
            "SELECT cols.COLUMN_NAME FROM USER_CONSTRAINTS cons " +
            "JOIN USER_CONS_COLUMNS cols ON cons.CONSTRAINT_NAME = cols.CONSTRAINT_NAME " +
            "WHERE cons.TABLE_NAME = :tbl AND cons.CONSTRAINT_TYPE = 'P' " +
            "ORDER BY cols.POSITION FETCH FIRST 1 ROW ONLY", _conn);
        cmd.Parameters.Add(new OracleParameter("tbl", table));

        var result = cmd.ExecuteScalar()?.ToString();
        if (result == null)
        {
            using var cmd2 = new OracleCommand(
                "SELECT COLUMN_NAME FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :tbl " +
                "ORDER BY COLUMN_ID FETCH FIRST 1 ROW ONLY", _conn);
            cmd2.Parameters.Add(new OracleParameter("tbl", table));
            result = cmd2.ExecuteScalar()?.ToString() ?? "id";
        }

        _pkCache[table] = result;
        return result;
    }

    public bool IsView(string table)
    {
        if (_viewCache.TryGetValue(table, out var cached)) return cached;

        using var cmd = new OracleCommand(
            "SELECT COUNT(*) FROM USER_VIEWS WHERE VIEW_NAME = :tbl", _conn);
        cmd.Parameters.Add(new OracleParameter("tbl", table));
        var isView = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

        _viewCache[table] = isView;
        return isView;
    }

    public List<ForeignKeyInfo> GetForeignKeys(string table)
    {
        if (_fkCache.TryGetValue(table, out var cached)) return cached;

        var fks = new List<ForeignKeyInfo>();
        using var cmd = new OracleCommand(
            "SELECT a.COLUMN_NAME, c_pk.TABLE_NAME, b.COLUMN_NAME " +
            "FROM USER_CONS_COLUMNS a " +
            "JOIN USER_CONSTRAINTS c ON a.CONSTRAINT_NAME = c.CONSTRAINT_NAME " +
            "JOIN USER_CONSTRAINTS c_pk ON c.R_CONSTRAINT_NAME = c_pk.CONSTRAINT_NAME " +
            "JOIN USER_CONS_COLUMNS b ON c_pk.CONSTRAINT_NAME = b.CONSTRAINT_NAME AND b.POSITION = a.POSITION " +
            "WHERE c.CONSTRAINT_TYPE = 'R' AND a.TABLE_NAME = :tbl", _conn);
        cmd.Parameters.Add(new OracleParameter("tbl", table));
        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            fks.Add(new ForeignKeyInfo
            {
                Column = r.GetString(0),
                RefTable = r.GetString(1),
                RefColumn = r.GetString(2)
            });
        }

        _fkCache[table] = fks;
        return fks;
    }

    private HashSet<string> GetIdentityColumns(string table)
    {
        if (_identityCache.TryGetValue(table, out var cached)) return cached;

        var set = new HashSet<string>();
        using var cmd = new OracleCommand(
            "SELECT COLUMN_NAME FROM USER_TAB_IDENTITY_COLS WHERE TABLE_NAME = :tbl", _conn);
        cmd.Parameters.Add(new OracleParameter("tbl", table));
        using var r = cmd.ExecuteReader();
        while (r.Read()) set.Add(r.GetString(0));

        _identityCache[table] = set;
        return set;
    }

    public string ResolveTable(string name)
    {
        using var cmd = new OracleCommand(
            "SELECT TABLE_NAME FROM (" +
            "  SELECT TABLE_NAME FROM USER_TABLES WHERE TABLE_NAME = :tbl " +
            "  UNION ALL " +
            "  SELECT VIEW_NAME FROM USER_VIEWS WHERE VIEW_NAME = :tbl2" +
            ") WHERE ROWNUM = 1", _conn);
        cmd.Parameters.Add(new OracleParameter("tbl", name));
        cmd.Parameters.Add(new OracleParameter("tbl2", name));

        var result = cmd.ExecuteScalar()?.ToString();
        if (result == null) throw new HttpException(404, $"Ressource '{name}' existiert nicht");
        if (!IsObjectEnabled(result)) throw new HttpException(404, $"Ressource '{name}' nicht freigegeben");
        return result;
    }

    public bool IsObjectEnabled(string name)
    {
        if (_config.ExcludedObjects.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        if (_config.EnabledObjects == "*") return true;
        return true;
    }

    public object? GetFkValues(string table, string column)
    {
        var fks = GetForeignKeys(table);
        var fk = fks.FirstOrDefault(f => f.Column == column);
        if (fk == null) throw new HttpException(404, $"Keine FK-Referenz fuer Spalte '{column}'");

        var refTable = fk.RefTable;
        var refCol = fk.RefColumn;
        var refCols = GetColumns(refTable);

        var allVarchar = refCols.Where(c =>
            c.Type is "varchar2" or "char" or "nvarchar2" && !c.Pk).ToList();
        var preferred = new[] { "name", "title", "first_name", "last_name", "bezeichnung", "label" };
        var displayCols = allVarchar.Where(c => preferred.Contains(c.Name)).ToList();
        if (displayCols.Count == 0) displayCols = allVarchar.Take(2).ToList();

        var selectParts = new List<string> { Q(refCol) };
        selectParts.AddRange(displayCols.Select(c => Q(c.Name)));

        var sql = $"SELECT {string.Join(",", selectParts)} FROM {Q(refTable)} " +
                  $"ORDER BY {Q(refCol)} FETCH FIRST 1000 ROWS ONLY";

        var items = new List<object>();
        using var cmd = new OracleCommand(sql, _conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var value = r[0];
            var labelParts = new List<string>();
            foreach (var dc in displayCols)
            {
                var v = r[dc.Name];
                if (v != null && v != DBNull.Value && !string.IsNullOrEmpty(v.ToString()))
                    labelParts.Add(v.ToString()!);
            }
            items.Add(new
            {
                value,
                label = labelParts.Count > 0 ? string.Join(" ", labelParts) : value?.ToString() ?? ""
            });
        }

        return new { column, ref_table = refTable, ref_column = refCol, items };
    }

    public void ClearCache()
    {
        _colCache.Clear();
        _pkCache.Clear();
        _viewCache.Clear();
        _fkCache.Clear();
        _identityCache.Clear();
    }

    public void ClearCacheFor(string table)
    {
        _colCache.Remove(table);
        _pkCache.Remove(table);
        _viewCache.Remove(table);
        _fkCache.Remove(table);
        _identityCache.Remove(table);
    }
}
