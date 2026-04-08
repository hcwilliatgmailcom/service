using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Service.Models;
using Service.Services;

namespace Service.Controllers;

[Route("api")]
public class CrudController : BaseApiController
{
    private readonly MetadataService _metadata;
    private readonly CrudService _crud;
    private readonly ServiceConfig _config;

    public CrudController(AuthorizationService authService, MetadataService metadata, CrudService crud, ServiceConfig config)
        : base(authService)
    {
        _metadata = metadata;
        _crud = crud;
        _config = config;
    }

    [HttpGet("{table}")]
    public IActionResult ListRows(string table)
    {
        var resolved = _metadata.ResolveTable(table);
        AuthService.RequireAccess(CurrentUser, resolved, "read");

        var queryParams = new Dictionary<string, string?>();
        foreach (var q in Request.Query)
            queryParams[q.Key] = q.Value.FirstOrDefault();

        var (rows, total) = _crud.ListRows(resolved, queryParams);

        var limit = _config.Limits.DefaultLimit;
        var offset = 0;
        if (queryParams.TryGetValue("limit", out var lim) && int.TryParse(lim, out var l))
            limit = Math.Max(1, Math.Min(l, _config.Limits.MaxLimit));
        if (queryParams.TryGetValue("offset", out var off) && int.TryParse(off, out var o))
            offset = Math.Max(0, o);

        var baseLink = $"{BaseUrl}/{Uri.EscapeDataString(resolved)}";
        var links = new Dictionary<string, string>
        {
            ["self"] = $"{baseLink}/?limit={limit}&offset={offset}"
        };
        if (offset + limit < total)
            links["next"] = $"{baseLink}/?limit={limit}&offset={offset + limit}";
        if (offset > 0)
            links["prev"] = $"{baseLink}/?limit={limit}&offset={Math.Max(0, offset - limit)}";

        return Ok(new
        {
            count = rows.Count,
            total,
            limit,
            offset,
            items = rows,
            links
        });
    }

    [HttpGet("{table}/{id}")]
    public IActionResult GetRow(string table, string id)
    {
        var resolved = _metadata.ResolveTable(table);
        AuthService.RequireAccess(CurrentUser, resolved, "read");

        var row = _crud.GetRow(resolved, id);
        if (row == null) return NotFound(new { error = $"Datensatz {id} nicht gefunden" });
        return Ok(row);
    }

    [HttpPost("{table}")]
    public IActionResult CreateRow(string table, [FromBody] Dictionary<string, JsonElement> body)
    {
        var resolved = _metadata.ResolveTable(table);
        AuthService.RequireAccess(CurrentUser, resolved, "write");

        var (row, newId) = _crud.CreateRow(resolved, body);

        if (newId != null)
        {
            Response.Headers.Location = $"{BaseUrl}/{Uri.EscapeDataString(resolved)}/{Uri.EscapeDataString(newId.ToString()!)}";
        }

        return StatusCode(201, row ?? new Dictionary<string, object?> { ["inserted"] = true, ["id"] = newId });
    }

    [HttpPut("{table}/{id}")]
    public IActionResult ReplaceRow(string table, string id, [FromBody] Dictionary<string, JsonElement> body)
    {
        var resolved = _metadata.ResolveTable(table);
        AuthService.RequireAccess(CurrentUser, resolved, "write");
        var row = _crud.UpdateRow(resolved, id, body);
        return Ok(row);
    }

    [HttpPatch("{table}/{id}")]
    public IActionResult PatchRow(string table, string id, [FromBody] Dictionary<string, JsonElement> body)
    {
        var resolved = _metadata.ResolveTable(table);
        AuthService.RequireAccess(CurrentUser, resolved, "write");
        var row = _crud.UpdateRow(resolved, id, body);
        return Ok(row);
    }

    [HttpDelete("{table}/{id}")]
    public IActionResult DeleteRow(string table, string id)
    {
        var resolved = _metadata.ResolveTable(table);
        AuthService.RequireAccess(CurrentUser, resolved, "write");
        _crud.DeleteRow(resolved, id);
        return NoContent();
    }
}
