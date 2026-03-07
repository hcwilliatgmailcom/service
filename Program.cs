using Cmdb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

var connStr = builder.Configuration.GetConnectionString("Oracle")
    ?? "User Id=cmdb;Password=cmdb123;Data Source=localhost:1521/XEPDB1;";

builder.Services.AddSingleton(new SchemaService(connStr));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// Auth guard: redirect to /login unless the session has an email or the path is /login or /auth
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var isPublic = path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

    if (!isPublic && context.Session.GetString("email") == null)
    {
        context.Response.Redirect("/login");
        return;
    }

    await next();
});

app.MapControllers();

app.Run();
