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

// Global error handler: catch unhandled exceptions, show them as a flash message
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        context.Session.SetString("_flash", $"danger|Error: {msg}");
        var referer = context.Request.Headers["Referer"].FirstOrDefault() ?? "/";
        context.Response.Redirect(referer);
    }
});

// Auth guard: redirect to /login unless the session has an email or the path is /login or /auth
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var isPublic = path == "/" || path == ""
                || path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
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
