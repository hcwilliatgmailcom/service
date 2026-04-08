namespace Service.Models;

public class ForeignKeyInfo
{
    public string Column { get; set; } = "";
    public string RefTable { get; set; } = "";
    public string RefColumn { get; set; } = "";
}
