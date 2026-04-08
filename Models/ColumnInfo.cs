namespace Service.Models;

public class ColumnInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Nullable { get; set; }
    public bool Pk { get; set; }
    public bool Auto { get; set; }
    public string? Default { get; set; }
}
