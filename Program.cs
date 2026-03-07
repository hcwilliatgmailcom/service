using Cmdb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

var connStr = builder.Configuration.GetConnectionString("Oracle")
    ?? "User Id=cmdb;Password=cmdb123;Data Source=localhost:1521/XEPDB1;";

builder.Services.AddSingleton(new SchemaService(connStr));

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();
