using System.Text.Json;
using Service.Exceptions;

namespace Service.Helpers;

public static class JsonHelpers
{
    public static string GetJsonString(Dictionary<string, JsonElement> d, string key)
    {
        if (!d.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
            throw new HttpException(400, $"Feld '{key}' erforderlich");
        return el.GetString()!;
    }

    public static string GetJsonString(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString()!;
        return "";
    }

    public static string? GetJsonStringOrNull(Dictionary<string, JsonElement> d, string key)
    {
        if (d.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    public static string? GetJsonStringOrNull(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    public static int GetJsonInt(Dictionary<string, JsonElement> d, string key, int def = 0)
    {
        if (d.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetInt32();
        return def;
    }

    public static int GetJsonInt(JsonElement el, string key, int def = 0)
    {
        if (el.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return def;
    }

    public static bool GetJsonBool(Dictionary<string, JsonElement> d, string key)
    {
        if (d.TryGetValue(key, out var el))
        {
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }

    public static bool GetJsonBool(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }
}
