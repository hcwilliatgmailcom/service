namespace Service.Models;

public class ServiceConfig
{
    public string ConnectionString { get; set; } = "";
    public string EnabledObjects { get; set; } = "*";
    public List<string> ExcludedObjects { get; set; } = new();
    public Dictionary<string, UserConfig> Users { get; set; } = new();
    public Dictionary<string, AclConfig> Acl { get; set; } = new();
    public CorsConfig Cors { get; set; } = new();
    public LimitsConfig Limits { get; set; } = new();
    public bool Debug { get; set; }
}

public class UserConfig
{
    public string Password { get; set; } = "";
    public List<string> Roles { get; set; } = new();
}

public class AclConfig
{
    public List<string> Read { get; set; } = new();
    public List<string> Write { get; set; } = new();
}

public class CorsConfig
{
    public bool Enabled { get; set; }
    public string Origin { get; set; } = "*";
    public string Methods { get; set; } = "GET, POST, PUT, PATCH, DELETE, OPTIONS";
    public string Headers { get; set; } = "Authorization, Content-Type";
}

public class LimitsConfig
{
    public int DefaultLimit { get; set; } = 25;
    public int MaxLimit { get; set; } = 500;
}
