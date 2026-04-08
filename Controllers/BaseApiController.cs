using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Exceptions;
using Service.Services;

namespace Service.Controllers;

[ApiController]
[Authorize]
public abstract class BaseApiController : ControllerBase
{
    protected string CurrentUser => User.Identity?.Name ?? "";
    protected string BaseUrl => $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api";

    protected readonly AuthorizationService AuthService;

    protected BaseApiController(AuthorizationService authService)
    {
        AuthService = authService;
    }

    protected void RequireWriteRole()
    {
        if (!AuthService.HasWriteRole(CurrentUser))
            throw new HttpException(403, "Schema-Aenderungen erfordern write-Rolle");
    }
}
