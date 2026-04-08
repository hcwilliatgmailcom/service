using Service.Models;
using Service.Exceptions;

namespace Service.Services;

public class AuthorizationService
{
    private readonly ServiceConfig _config;

    public AuthorizationService(ServiceConfig config)
    {
        _config = config;
    }

    public void RequireAccess(string user, string table, string mode)
    {
        if (!_config.Users.TryGetValue(user, out var userCfg)) throw new HttpException(403, "Unbekannter User");
        var userRoles = userCfg.Roles;

        if (_config.Acl.TryGetValue(table, out var acl))
        {
            var allowed = mode == "read" ? acl.Read : acl.Write;
            if (!userRoles.Intersect(allowed).Any())
                throw new HttpException(403, $"Keine Berechtigung ({mode}) fuer {table}");
            return;
        }

        var needed = mode == "read" ? "read" : "write";
        if (!userRoles.Contains(needed))
            throw new HttpException(403, $"Keine Berechtigung ({mode}) fuer {table}");
    }

    public bool HasWriteRole(string user)
    {
        return _config.Users.TryGetValue(user, out var u) && u.Roles.Contains("write");
    }
}
