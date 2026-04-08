using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using Service.Models;
using Service.Exceptions;
using static Service.Helpers.OracleHelpers;
using static Service.Helpers.JsonHelpers;

namespace Service.Services;

public class SchemaService
{
    private readonly OracleConnection _conn;
    private readonly MetadataService _metadata;

    public SchemaService(OracleConnection conn, MetadataService metadata)
    {
        _conn = conn;
        _metadata = metadata;
    }

    public string CreateTable(Dictionary<string, JsonElement> body)
    {
        var name = GetJsonString(body, "name");
        ValidateIdentifier(name);

        using (var chk = new OracleCommand(
            "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = :tbl", _conn))
        {
            chk.Parameters.Add(new OracleParameter("tbl", name));
            if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                throw new HttpException(409, $"Tabelle '{name}' existiert bereits");
        }

        if (!body.TryGetValue("columns", out var colsEl) || colsEl.ValueKind != JsonValueKind.Array)
            throw new HttpException(400, "Mindestens eine Spalte erforderlich");

        var colDefs = new List<string>();
        string? pkCol = null;

        foreach (var c in colsEl.EnumerateArray())
        {
            var cName = GetJsonString(c, "name");
            ValidateIdentifier(cName);
            var cType = GetJsonString(c, "type");
            var length = GetJsonInt(c, "length");
            var isPk = GetJsonBool(c, "pk");
            var notNull = GetJsonBool(c, "not_null");

            var oraType = MapColumnType(cType, length);
            var parts = new List<string> { Q(cName), oraType };

            if (isPk)
            {
                pkCol = cName;
                if (cType is "int" or "integer" or "bigint")
                    parts.Add("GENERATED ALWAYS AS IDENTITY");
            }
            if (notNull && !isPk) parts.Add("NOT NULL");

            colDefs.Add(string.Join(" ", parts));
        }

        if (pkCol == null)
        {
            colDefs.Insert(0, Q("id") + " NUMBER GENERATED ALWAYS AS IDENTITY");
            pkCol = "id";
        }

        colDefs.Add($"CONSTRAINT pk_{name} PRIMARY KEY ({Q(pkCol)})");

        var sql = $"CREATE TABLE {Q(name)} (\n  {string.Join(",\n  ", colDefs)}\n)";
        using var cmd = new OracleCommand(sql, _conn);
        cmd.ExecuteNonQuery();
        _metadata.ClearCache();

        return sql;
    }

    public void DropTable(string name)
    {
        var table = _metadata.ResolveTable(name);
        if (_metadata.IsView(table)) throw new HttpException(400, "Views koennen hier nicht geloescht werden");

        using var cmd = new OracleCommand($"DROP TABLE {Q(table)} CASCADE CONSTRAINTS", _conn);
        cmd.ExecuteNonQuery();
        _metadata.ClearCache();
    }

    public string AddColumn(string table, Dictionary<string, JsonElement> body)
    {
        table = _metadata.ResolveTable(table);
        if (_metadata.IsView(table)) throw new HttpException(400, "Views koennen nicht geaendert werden");

        var cName = GetJsonString(body, "name");
        ValidateIdentifier(cName);
        var cType = GetJsonString(body, "type");
        var length = GetJsonInt(body, "length");
        var notNull = GetJsonBool(body, "not_null");
        var def = GetJsonStringOrNull(body, "default");

        var existing = _metadata.GetColumns(table).Select(c => c.Name).ToHashSet();
        if (existing.Contains(cName)) throw new HttpException(409, $"Spalte '{cName}' existiert bereits");

        var oraType = MapColumnType(cType, length);
        var colDef = $"{Q(cName)} {oraType}";
        if (!string.IsNullOrEmpty(def))
        {
            colDef += def.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)
                ? " DEFAULT CURRENT_TIMESTAMP"
                : $" DEFAULT '{def.Replace("'", "''")}'";
        }
        if (notNull) colDef += " NOT NULL";

        var sql = $"ALTER TABLE {Q(table)} ADD ({colDef})";
        using var cmd = new OracleCommand(sql, _conn);
        cmd.ExecuteNonQuery();
        _metadata.ClearCacheFor(table);

        return sql;
    }

    public string ModifyColumn(string table, string colName, Dictionary<string, JsonElement> body)
    {
        table = _metadata.ResolveTable(table);
        if (_metadata.IsView(table)) throw new HttpException(400, "Views koennen nicht geaendert werden");

        var existing = _metadata.GetColumns(table).Select(c => c.Name).ToHashSet();
        if (!existing.Contains(colName)) throw new HttpException(404, $"Spalte '{colName}' nicht gefunden");

        var newName = GetJsonStringOrNull(body, "new_name") ?? colName;
        var cType = GetJsonString(body, "type");
        var length = GetJsonInt(body, "length");
        var notNull = GetJsonBool(body, "not_null");

        var oraType = MapColumnType(cType, length);
        var modDef = $"{Q(colName)} {oraType}";
        if (notNull) modDef += " NOT NULL";

        var sql = $"ALTER TABLE {Q(table)} MODIFY ({modDef})";
        using var cmd = new OracleCommand(sql, _conn);
        cmd.ExecuteNonQuery();

        if (newName != colName)
        {
            ValidateIdentifier(newName);
            var renameSql = $"ALTER TABLE {Q(table)} RENAME COLUMN {Q(colName)} TO {Q(newName)}";
            using var cmd2 = new OracleCommand(renameSql, _conn);
            cmd2.ExecuteNonQuery();
            sql += "; " + renameSql;
        }

        _metadata.ClearCacheFor(table);
        return sql;
    }

    public void DropColumn(string table, string colName)
    {
        table = _metadata.ResolveTable(table);
        if (_metadata.IsView(table)) throw new HttpException(400, "Views koennen nicht geaendert werden");

        var cols = _metadata.GetColumns(table);
        if (!cols.Any(c => c.Name == colName))
            throw new HttpException(404, $"Spalte '{colName}' nicht gefunden");
        if (cols.First(c => c.Name == colName).Pk)
            throw new HttpException(400, $"Primary Key '{colName}' kann nicht geloescht werden");

        var fks = _metadata.GetForeignKeys(table);
        foreach (var fk in fks.Where(f => f.Column == colName))
            DropFkConstraint(table, colName);

        using var cmd = new OracleCommand(
            $"ALTER TABLE {Q(table)} DROP COLUMN {Q(colName)}", _conn);
        cmd.ExecuteNonQuery();
        _metadata.ClearCacheFor(table);
    }

    public string AddFk(string table, Dictionary<string, JsonElement> body)
    {
        table = _metadata.ResolveTable(table);
        var col = GetJsonString(body, "column");
        var refTable = GetJsonString(body, "ref_table");
        var refCol = GetJsonString(body, "ref_column");

        ValidateIdentifier(col);
        ValidateIdentifier(refTable);
        ValidateIdentifier(refCol);

        var existing = _metadata.GetColumns(table).Select(c => c.Name).ToHashSet();
        if (!existing.Contains(col)) throw new HttpException(404, $"Spalte '{col}' nicht gefunden");
        _metadata.ResolveTable(refTable);

        var fkName = $"fk_{table}_{col}";
        var sql = $"ALTER TABLE {Q(table)} ADD CONSTRAINT {Q(fkName)} " +
                  $"FOREIGN KEY ({Q(col)}) REFERENCES {Q(refTable)}({Q(refCol)})";

        using var cmd = new OracleCommand(sql, _conn);
        cmd.ExecuteNonQuery();
        _metadata.ClearCacheFor(table);

        return sql;
    }

    public void DropFk(string table, string col)
    {
        table = _metadata.ResolveTable(table);
        DropFkConstraint(table, col);
        _metadata.ClearCacheFor(table);
    }

    private void DropFkConstraint(string table, string col)
    {
        using var cmd = new OracleCommand(
            "SELECT c.CONSTRAINT_NAME FROM USER_CONS_COLUMNS a " +
            "JOIN USER_CONSTRAINTS c ON a.CONSTRAINT_NAME = c.CONSTRAINT_NAME " +
            "WHERE c.CONSTRAINT_TYPE = 'R' AND a.TABLE_NAME = :tbl AND a.COLUMN_NAME = :col " +
            "FETCH FIRST 1 ROW ONLY", _conn);
        cmd.Parameters.Add(new OracleParameter("tbl", table));
        cmd.Parameters.Add(new OracleParameter("col", col));

        var cName = cmd.ExecuteScalar()?.ToString();
        if (cName == null) throw new HttpException(404, $"Kein FK-Constraint auf Spalte '{col}'");

        using var drop = new OracleCommand(
            $"ALTER TABLE {Q(table)} DROP CONSTRAINT {Q(cName)}", _conn);
        drop.ExecuteNonQuery();
    }
}
