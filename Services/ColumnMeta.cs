namespace Cmdb.Services;

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
