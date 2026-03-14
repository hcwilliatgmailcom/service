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

// Global error handler
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        if (context.Response.HasStarted) return;
        var msg = ex.InnerException?.Message ?? ex.Message;
        var current = context.Request.Path.Value ?? "/";
        var referer = context.Request.Headers["Referer"].FirstOrDefault() ?? "";

        // Pick a safe redirect target that is not the current page
        var target = (!string.IsNullOrEmpty(referer) && !referer.Contains(current)) ? referer : null;

        try { context.Session.SetString("_flash", $"danger|Error: {msg}"); } catch { }

        if (target != null)
        {
            context.Response.Redirect(target);
        }
        else
        {
            // Cannot redirect safely — write error inline
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync($"<div style='font-family:sans-serif;padding:2em;color:red'><b>Error:</b> {System.Net.WebUtility.HtmlEncode(msg)}</div>");
        }
    }
});

// Auth guard
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var isPublic = path == "/" || path == ""
                || path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

    if (!isPublic)
    {
        string? email = null;
        try { email = context.Session.GetString("email"); } catch { }
        if (email == null)
        {
            context.Response.Redirect("/");
            return;
        }
    }

    await next();
});

app.MapControllers();

app.Run();
