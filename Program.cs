using Microsoft.AspNetCore.Authentication;
using Oracle.ManagedDataAccess.Client;
using Service.Auth;
using Service.Filters;
using Service.Models;
using Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var serviceSection = builder.Configuration.GetSection("Service");
var config = new ServiceConfig
{
    ConnectionString = builder.Configuration.GetConnectionString("Oracle") ?? "",
    EnabledObjects = serviceSection["EnabledObjects"] ?? "*",
    ExcludedObjects = serviceSection.GetSection("ExcludedObjects").Get<List<string>>() ?? new(),
    Debug = serviceSection.GetValue<bool>("Debug"),
    Cors = serviceSection.GetSection("Cors").Get<CorsConfig>() ?? new(),
    Limits = serviceSection.GetSection("Limits").Get<LimitsConfig>() ?? new(),
};

// Parse Users
var usersSection = serviceSection.GetSection("Users");
foreach (var child in usersSection.GetChildren())
{
    config.Users[child.Key] = new UserConfig
    {
        Password = child["Password"] ?? "",
        Roles = child.GetSection("Roles").Get<List<string>>() ?? new()
    };
}

// Parse ACL
var aclSection = serviceSection.GetSection("Acl");
foreach (var child in aclSection.GetChildren())
{
    config.Acl[child.Key] = new AclConfig
    {
        Read = child.GetSection("Read").Get<List<string>>() ?? new(),
        Write = child.GetSection("Write").Get<List<string>>() ?? new()
    };
}

builder.Services.AddSingleton(config);

// Database connection (scoped - auto-disposed per request)
builder.Services.AddScoped(sp =>
{
    var cfg = sp.GetRequiredService<ServiceConfig>();
    var conn = new OracleConnection(cfg.ConnectionString);
    conn.Open();
    return conn;
});

// Services
builder.Services.AddScoped<MetadataService>();
builder.Services.AddScoped<CrudService>();
builder.Services.AddScoped<SchemaService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<OpenApiService>();
builder.Services.AddScoped<AuthorizationService>();

builder.Services.AddControllers(opts =>
    {
        opts.Filters.Add<HttpExceptionFilter>();
    })
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Authentication
builder.Services.AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("Basic", null);
builder.Services.AddAuthorization();

// CORS
if (config.Cors.Enabled)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (config.Cors.Origin == "*")
                policy.AllowAnyOrigin();
            else
                policy.WithOrigins(config.Cors.Origin);

            policy.WithMethods(config.Cors.Methods.Split(',', StringSplitOptions.TrimEntries))
                  .WithHeaders(config.Cors.Headers.Split(',', StringSplitOptions.TrimEntries));
        });
    });
}

var app = builder.Build();

// Seed: create default table if schema is empty
using (var scope = app.Services.CreateScope())
{
    var conn = scope.ServiceProvider.GetRequiredService<OracleConnection>();
    using var countCmd = new OracleCommand("SELECT COUNT(*) FROM USER_TABLES", conn);
    var tableCount = Convert.ToInt32(countCmd.ExecuteScalar());
    if (tableCount == 0)
    {
        using var createCmd = new OracleCommand(
            "CREATE TABLE \"PERSON\" (" +
            "  \"ID\" NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY," +
            "  \"NAME\" VARCHAR2(100) NOT NULL" +
            ")", conn);
        createCmd.ExecuteNonQuery();
    }
}

if (config.Cors.Enabled)
    app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
