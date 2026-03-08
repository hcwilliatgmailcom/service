using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;

public class LoginController : Controller
{
    // In-memory token store: token -> (email, expiry)
    private static readonly Dictionary<string, (string Email, DateTime Expiry)> _tokens = new();
    private static readonly object _lock = new();

    private readonly ILogger<LoginController> _logger;
    private readonly IConfiguration _config;

    public LoginController(ILogger<LoginController> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    // GET /login
    [HttpGet("/login")]
    public IActionResult Index()
    {
        if (HttpContext.Session.GetString("email") != null)
            return Redirect("/");
        return View("Index");
    }

    // POST /login
    [HttpPost("/login")]
    public async Task<IActionResult> Send()
    {
        var email = Request.Form["email"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrEmpty(email))
        {
            TempData["Flash"] = "danger|Please enter your email address.";
            return Redirect("/login");
        }

        var token = Guid.NewGuid().ToString("N");
        var expiry = DateTime.UtcNow.AddMinutes(15);

        lock (_lock)
        {
            _tokens[token] = (email, expiry);
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var link = $"{baseUrl}/auth?token={token}";

        _logger.LogInformation("Magic link for {Email}: {Link}", email, link);

        var isLocalhost = Request.Host.Host is "localhost" or "127.0.0.1" or "::1";
        if (isLocalhost)
        {
            TempData["MagicLink"] = link;
            TempData["Flash"] = "success|Your login link is shown below.";
        }
        else
        {
            try
            {
                await SendMagicLinkEmail(email, link);
                TempData["Flash"] = "success|Check your email for the login link.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send magic link email to {Email}", email);
                TempData["Flash"] = "danger|Could not send email. Contact an administrator.";
            }
        }

        return Redirect("/login");
    }

    // GET /auth?token=...
    [HttpGet("/auth")]
    public IActionResult Auth(string token)
    {
        if (string.IsNullOrEmpty(token))
            return Redirect("/login");

        (string Email, DateTime Expiry) entry;
        bool found;

        lock (_lock)
        {
            found = _tokens.TryGetValue(token, out entry);
            if (found) _tokens.Remove(token);
        }

        if (!found || DateTime.UtcNow > entry.Expiry)
        {
            TempData["Flash"] = "danger|This login link is invalid or has expired.";
            return Redirect("/login");
        }

        HttpContext.Session.SetString("email", entry.Email);
        return Redirect("/");
    }

    // GET /logout
    [HttpGet("/logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Redirect("/login");
    }

    private async Task SendMagicLinkEmail(string to, string link)
    {
        var smtp = _config.GetSection("Smtp");
        var host = smtp["Host"] ?? throw new InvalidOperationException("Smtp:Host not configured");
        var port = smtp.GetValue("Port", 587);
        var user = smtp["User"];
        var pass = smtp["Password"];
        var from = smtp["From"] ?? user ?? "no-reply@localhost";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = !string.IsNullOrEmpty(user) ? new NetworkCredential(user, pass) : null
        };

        var body = $"""
            <p>Click the link below to sign in to CMDB. This link expires in 15 minutes.</p>
            <p><a href="{link}">{link}</a></p>
            """;

        using var msg = new MailMessage(from, to)
        {
            Subject = "Your CMDB login link",
            Body = body,
            IsBodyHtml = true,
        };

        await client.SendMailAsync(msg);
    }
}
