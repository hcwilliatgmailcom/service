using Microsoft.AspNetCore.Mvc;

public class LoginController : Controller
{
    // In-memory token store: token -> (email, expiry)
    private static readonly Dictionary<string, (string Email, DateTime Expiry)> _tokens = new();
    private static readonly object _lock = new();

    private readonly ILogger<LoginController> _logger;

    public LoginController(ILogger<LoginController> logger)
    {
        _logger = logger;
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
    public IActionResult Send()
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
            TempData["MagicLink"] = link;

        TempData["Flash"] = isLocalhost
            ? "success|Your login link is shown below."
            : "success|Check the server log for your login link.";
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
}
