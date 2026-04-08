using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Service.Models;

namespace Service.Auth;

public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ServiceConfig _config;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ServiceConfig config)
        : base(options, logger, encoder)
    {
        _config = config;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        try
        {
            var encoded = authHeader["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var sep = decoded.IndexOf(':');
            if (sep < 0)
                return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));

            var user = decoded[..sep];
            var pass = decoded[(sep + 1)..];

            if (!_config.Users.TryGetValue(user, out var userCfg))
                return Task.FromResult(AuthenticateResult.Fail("Unknown user"));

            if (!BCrypt.Net.BCrypt.Verify(pass, userCfg.Password))
                return Task.FromResult(AuthenticateResult.Fail("Invalid password"));

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user)
            };
            foreach (var role in userCfg.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid authorization header"));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"Service\"";
        return base.HandleChallengeAsync(properties);
    }
}
