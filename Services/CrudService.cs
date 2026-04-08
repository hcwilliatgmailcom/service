using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using Service.Models;
using Service.Exceptions;
using static Service.Helpers.OracleHelpers;

namespace Service.Services;

public class CrudService
{
    private readonly OracleConnection _conn;
    private readonly ServiceConfig _config;
    private readonly MetadataService _metadata;

    public CrudService(OracleConnection conn, ServiceConfig config, MetadataService metadata)
    {
        _conn = conn;
        _config = config;
        _metadata = metadata;
    }

    public (List<Dictionary<string, object?>> rows, int total) ListRows(
        string table, Dictionary<string, string?> queryParams)
    {
        var columns = _metadata.GetColumns(table);
        var colNames = columns.Select(c => c.Name).ToList();

        var limitVal = _config.Limits.DefaultLimit;
        var offsetVal = 0;

        if (queryParams.TryGetValue("limit", out var lim) && int.TryParse(lim, out var l))
            limitVal = Math.Max(1, Math.Min(l, _config.Limits.MaxLimit));
        if (queryParams.TryGetValue("offset", out var off) && int.TryParse(off, out var o))
            offsetVal = Math.Max(0, o);

        var selectCols = "*";
        if (queryParams.TryGetValue("fields", out var fields) && !string.IsNullOrEmpty(fields))
        {
            var requested = fields.Split(',', StringSplitOptions.TrimEntries);
            var valid = requested.Where(f => colNames.Contains(f)).ToList();
            if (valid.Count == 0) throw new HttpException(400, "Keine gueltigen Spalten in fields=");
            selectCols = string.Join(",", valid.Select(Q));
        }

        var (whereClauses, bindings) = BuildWhere(queryParams, colNames);

        if (queryParams.TryGetValue("q", out var q) && !string.IsNullOrEmpty(q))
        {
            var textCols = columns.Where(c =>
                c.Type is "varchar2" or "char" or "nvarchar2" or "clob" or "nclob").ToList();
            if (textCols.Count > 0)
            {
                var orParts = new List<string>();
                foreach (var c in textCols)
                {
                    var ph = $":q_{c.Name}";
                    orParts.Add($"{Q(c.Name)} LIKE {ph}");
                    bindings[ph] = $"%{q}%";
                }
                whereClauses.Add("(" + string.Join(" OR ", orParts) + ")");
            }
        }

        var orderSql = "";
        if (queryParams.TryGetValue("order", out var order) && !string.IsNullOrEmpty(order))
        {
            var parts = new List<string>();
            foreach (var token in order.Split(',', StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrEmpty(token)) continue;
                var split = token.Split('.', 2);
                var col = split[0];
                var dir = split.Length > 1 && split[1].Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (!colNames.Contains(col)) throw new HttpException(400, $"Unbekannte Sortierspalte: {col}");
                parts.Add($"{Q(col)} {dir}");
            }
            if (parts.Count > 0) orderSql = " ORDER BY " + string.Join(", ", parts);
        }

        var whereSql = whereClauses.Count > 0 ? " WHERE " + string.Join(" AND ", whereClauses) : "";
        var tbl = Q(table);

        if (string.IsNullOrEmpty(orderSql))
        {
            try { orderSql = $" ORDER BY {Q(_metadata.GetPrimaryKey(table))}"; }
            catch { orderSql = " ORDER BY ROWID"; }
        }

        int total;
        using (var cmd = new OracleCommand($"SELECT COUNT(*) FROM {tbl}{whereSql}", _conn))
        {
            AddBindings(cmd, bindings);
            total = Convert.ToInt32(cmd.ExecuteScalar());
        }

        var sql = $"SELECT {selectCols} FROM {tbl}{whereSql}{orderSql} OFFSET :pOffset ROWS FETCH NEXT :pLimit ROWS ONLY";
        var rows = new List<Dictionary<string, object?>>();
        using (var cmd = new OracleCommand(sql, _conn))
        {
            AddBindings(cmd, bindings);
            cmd.Parameters.Add(new OracleParameter("pOffset", offsetVal));
            cmd.Parameters.Add(new OracleParameter("pLimit", limitVal));

            using var r = cmd.ExecuteReader();
            while (r.Read())
                rows.Add(ReadRow(r));
        }

        return (rows, total);
    }

    public Dictionary<string, object?>? GetRow(string table, string id)
    {
        var pk = _metadata.GetPrimaryKey(table);
        var sql = $"SELECT * FROM {Q(table)} WHERE {Q(pk)} = :id FETCH FIRST 1 ROW ONLY";

        using var cmd = new OracleCommand(sql, _conn);
        cmd.Parameters.Add(new OracleParameter("id", id));
        using var r = cmd.ExecuteReader();

        return r.Read() ? ReadRow(r) : null;
    }

    public (Dictionary<string, object?>? row, object? newId) CreateRow(string table, Dictionary<string, JsonElement> body)
    {
        if (_metadata.IsView(table)) throw new HttpException(405, "Views sind schreibgeschuetzt");

        var columns = _metadata.GetColumns(table);
        var colNames = columns.Select(c => c.Name).ToHashSet();
        var pk = _metadata.GetPrimaryKey(table);
        var pkCol = columns.FirstOrDefault(c => c.Pk);

        var insertCols = new List<string>();
        var placeholders = new List<string>();
        var bindings = new Dictionary<string, object?>();
        var i = 0;

        foreach (var kv in body)
        {
            if (!colNames.Contains(kv.Key)) continue;
            var col = columns.First(c => c.Name == kv.Key);
            if (col.Auto) continue;

            var ph = $":p{i++}";
            insertCols.Add(Q(kv.Key));
            placeholders.Add(ph);
            bindings[ph] = JsonElementToValue(kv.Value);
        }

        if (insertCols.Count == 0) throw new HttpException(400, "Keine gueltigen Spalten im Body");

        var tbl = Q(table);
        var sql = $"INSERT INTO {tbl} ({string.Join(",", insertCols)}) VALUES ({string.Join(",", placeholders)})";

        object? newId = null;
        if (pkCol?.Auto == true)
        {
            sql += $" RETURNING {Q(pk)} INTO :newId";
            using var cmd = new OracleCommand(sql, _conn);
            AddBindings(cmd, bindings);
            var outParam = new OracleParameter("newId", OracleDbType.Decimal)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outParam);
            cmd.ExecuteNonQuery();
            var rawId = outParam.Value;
            if (rawId is Oracle.ManagedDataAccess.Types.OracleDecimal oraId && !oraId.IsNull)
                newId = ((long)oraId.Value).ToString();
            else
                newId = rawId?.ToString();
        }
        else
        {
            using var cmd = new OracleCommand(sql, _conn);
            AddBindings(cmd, bindings);
            cmd.ExecuteNonQuery();
            newId = body.TryGetValue(pk, out var pkVal) ? JsonElementToValue(pkVal) : null;
        }

        if (newId != null)
        {
            var row = GetRow(table, newId.ToString()!);
            return (row, newId);
        }
        return (null, newId);
    }

    public Dictionary<string, object?>? UpdateRow(string table, string id, Dictionary<string, JsonElement> body)
    {
        if (_metadata.IsView(table)) throw new HttpException(405, "Views sind schreibgeschuetzt");

        var columns = _metadata.GetColumns(table);
        var colNames = columns.Select(c => c.Name).ToHashSet();
        var pk = _metadata.GetPrimaryKey(table);

        var sets = new List<string>();
        var bindings = new Dictionary<string, object?>();
        var i = 0;

        foreach (var kv in body)
        {
            if (!colNames.Contains(kv.Key)) continue;
            if (kv.Key == pk) continue;
            var col = columns.First(c => c.Name == kv.Key);
            if (col.Auto) continue;

            var ph = $":p{i++}";
            sets.Add($"{Q(kv.Key)} = {ph}");
            bindings[ph] = JsonElementToValue(kv.Value);
        }

        if (sets.Count == 0) throw new HttpException(400, "Keine aenderbaren Spalten im Body");

        bindings[":__id"] = id;
        var sql = $"UPDATE {Q(table)} SET {string.Join(", ", sets)} WHERE {Q(pk)} = :__id";

        using (var cmd = new OracleCommand(sql, _conn))
        {
            AddBindings(cmd, bindings);
            var affected = cmd.ExecuteNonQuery();
            if (affected == 0)
            {
                var exists = GetRow(table, id);
                if (exists == null) throw new HttpException(404, $"Datensatz {id} nicht gefunden");
            }
        }

        return GetRow(table, id);
    }

    public void DeleteRow(string table, string id)
    {
        if (_metadata.IsView(table)) throw new HttpException(405, "Views sind schreibgeschuetzt");

        var pk = _metadata.GetPrimaryKey(table);
        using var cmd = new OracleCommand($"DELETE FROM {Q(table)} WHERE {Q(pk)} = :id", _conn);
        cmd.Parameters.Add(new OracleParameter("id", id));

        if (cmd.ExecuteNonQuery() == 0)
            throw new HttpException(404, $"Datensatz {id} nicht gefunden");
    }

    private (List<string> clauses, Dictionary<string, object?> bindings) BuildWhere(
        Dictionary<string, string?> p, List<string> colNames)
    {
        var reserved = new HashSet<string> { "limit", "offset", "order", "fields", "q" };
        var where = new List<string>();
        var bindings = new Dictionary<string, object?>();
        var i = 0;

        foreach (var kv in p)
        {
            if (reserved.Contains(kv.Key)) continue;
            if (!colNames.Contains(kv.Key)) continue;
            if (kv.Value == null) continue;

            var ph = $":w{++i}";
            var col = Q(kv.Key);
            var val = kv.Value;

            var match = Regex.Match(val, @"^(eq|ne|gt|gte|lt|lte|like):(.*)$", RegexOptions.Singleline);
            if (match.Success)
            {
                var op = match.Groups[1].Value switch
                {
                    "eq" => "=", "ne" => "<>", "gt" => ">", "gte" => ">=",
                    "lt" => "<", "lte" => "<=", "like" => "LIKE",
                    _ => "="
                };
                where.Add($"{col} {op} {ph}");
                bindings[ph] = match.Groups[2].Value;
            }
            else
            {
                where.Add($"{col} = {ph}");
                bindings[ph] = val;
            }
        }

        return (where, bindings);
    }
}
