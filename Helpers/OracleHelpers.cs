using System.Text.Json;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using Service.Exceptions;

namespace Service.Helpers;

public static class OracleHelpers
{
    public static string Q(string ident) => $"\"{ident.Replace("\"", "\"\"")}\"";

    public static Dictionary<string, object?> ReadRow(OracleDataReader r)
    {
        var row = new Dictionary<string, object?>();
        for (var i = 0; i < r.FieldCount; i++)
        {
            var name = r.GetName(i);
            row[name] = r.IsDBNull(i) ? null : ConvertOracleValue(r.GetValue(i));
        }
        return row;
    }

    public static object? ConvertOracleValue(object val)
    {
        if (val is Oracle.ManagedDataAccess.Types.OracleDecimal od)
        {
            if (od.IsNull) return null;
            var d = od.Value;
            return d == Math.Truncate(d) ? (object)(long)d : d;
        }
        if (val is Oracle.ManagedDataAccess.Types.OracleString os)
            return os.IsNull ? null : os.Value;
        if (val is Oracle.ManagedDataAccess.Types.OracleDate odt)
            return odt.IsNull ? null : odt.Value.ToString("yyyy-MM-dd");
        if (val is Oracle.ManagedDataAccess.Types.OracleTimeStamp ots)
            return ots.IsNull ? null : ots.Value.ToString("yyyy-MM-dd HH:mm:ss");
        if (val is decimal d2)
            return d2 == Math.Truncate(d2) ? (object)(long)d2 : d2;
        if (val is DateTime dt)
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        return val;
    }

    public static void AddBindings(OracleCommand cmd, Dictionary<string, object?> bindings)
    {
        cmd.BindByName = true;
        foreach (var kv in bindings)
            cmd.Parameters.Add(new OracleParameter(kv.Key.TrimStart(':'), kv.Value ?? DBNull.Value));
    }

    public static object? JsonElementToValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? (object)l : el.GetDecimal(),
        JsonValueKind.True => 1,
        JsonValueKind.False => 0,
        JsonValueKind.Null => null,
        _ => el.GetRawText()
    };

    public static string MapColumnType(string type, int length = 0) => type.ToLower() switch
    {
        "int" or "integer" => "NUMBER(10)",
        "bigint" => "NUMBER(19)",
        "smallint" => "NUMBER(5)",
        "tinyint" => "NUMBER(3)",
        "decimal" => "NUMBER(10,2)",
        "float" => "BINARY_FLOAT",
        "double" => "BINARY_DOUBLE",
        "varchar" => $"VARCHAR2({(length > 0 ? length : 255)})",
        "char" => $"CHAR({(length > 0 ? length : 1)})",
        "text" or "mediumtext" or "longtext" => "CLOB",
        "date" => "DATE",
        "datetime" or "timestamp" => "TIMESTAMP",
        "time" => "VARCHAR2(8)",
        "boolean" => "NUMBER(1)",
        "json" => "CLOB",
        "blob" => "BLOB",
        _ => throw new HttpException(400, $"Unbekannter Spaltentyp: '{type}'")
    };

    public static string MapJsonType(string oracleType) => oracleType.ToLower() switch
    {
        "number" or "binary_float" or "binary_double" or "float" => "number",
        _ => "string"
    };

    public static void ValidateIdentifier(string name)
    {
        if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]{0,63}$"))
            throw new HttpException(400, $"Ungueltiger Bezeichner: '{name}'");
    }
}
