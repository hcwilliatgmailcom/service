using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Service.Services;

namespace Service.Controllers;

[Route("api/_import")]
public class ImportController : BaseApiController
{
    private readonly ImportService _import;

    public ImportController(AuthorizationService authService, ImportService import)
        : base(authService)
    {
        _import = import;
    }

    [HttpGet("drivers")]
    public IActionResult GetImportDrivers()
    {
        RequireWriteRole();
        return Ok(_import.GetImportDrivers());
    }

    [HttpPost("test")]
    public IActionResult TestImportConnection([FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        return Ok(_import.TestExternalConnection(body));
    }

    [HttpPost("tables")]
    public IActionResult GetImportTables([FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        return Ok(_import.GetExternalTables(body));
    }

    [HttpPost("columns")]
    public IActionResult GetImportColumns([FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        return Ok(_import.GetExternalColumns(body));
    }

    [HttpPost("preview")]
    public IActionResult PreviewImport([FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        return Ok(_import.PreviewExternalData(body));
    }

    [HttpPost("sync")]
    public IActionResult SyncImport([FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        return Ok(_import.SyncData(body));
    }
}
