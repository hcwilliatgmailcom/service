using Microsoft.AspNetCore.Mvc;
using Service.Services;

namespace Service.Controllers;

[Route("api")]
public class ResourceController : BaseApiController
{
    private readonly MetadataService _metadata;

    public ResourceController(AuthorizationService authService, MetadataService metadata)
        : base(authService)
    {
        _metadata = metadata;
    }

    [HttpGet("")]
    public IActionResult ListResources()
    {
        var resources = _metadata.ListResources();
        var items = resources.Select(r => new
        {
            name = r.Name,
            type = r.Type,
            href = $"{BaseUrl}/{Uri.EscapeDataString(r.Name)}/",
            metadata = $"{BaseUrl}/_metadata/{Uri.EscapeDataString(r.Name)}"
        });
        return Ok(new { schema = "service", resources = items });
    }
}
