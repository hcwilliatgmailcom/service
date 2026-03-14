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
