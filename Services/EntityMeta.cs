namespace Cmdb.Services;

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

/// <summary>
/// Minimal stub retained so that views can continue to call SchemaService.SplitPascal(...)
/// without modification. All other functionality has been merged into HomeController.
/// </summary>
public static class SchemaService
{
    public static string SplitPascal(string name)
    {
        name = name.Replace("_", " ");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", " ");
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
    }
}
