using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Service.Services;

namespace Service.Controllers;

[Route("api/_schema")]
public class SchemaController : BaseApiController
{
    private readonly SchemaService _schema;

    public SchemaController(AuthorizationService authService, SchemaService schema)
        : base(authService)
    {
        _schema = schema;
    }

    [HttpPost("tables")]
    public IActionResult CreateTable([FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        var sql = _schema.CreateTable(body);
        var name = body.TryGetValue("name", out var n) ? n.GetString() : "";
        return StatusCode(201, new { message = $"Tabelle '{name}' angelegt", table = name, sql });
    }

    [HttpDelete("tables/{name}")]
    public IActionResult DropTable(string name)
    {
        RequireWriteRole();
        _schema.DropTable(name);
        return NoContent();
    }

    [HttpPost("{table}/columns")]
    public IActionResult AddColumn(string table, [FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        var sql = _schema.AddColumn(table, body);
        var cName = body.TryGetValue("name", out var n) ? n.GetString() : "";
        return StatusCode(201, new { message = $"Spalte '{cName}' zu '{table}' hinzugefuegt", sql });
    }

    [HttpPatch("{table}/columns/{column}")]
    public IActionResult ModifyColumn(string table, string column, [FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        var sql = _schema.ModifyColumn(table, column, body);
        return Ok(new { message = $"Spalte '{column}' geaendert", sql });
    }

    [HttpDelete("{table}/columns/{column}")]
    public IActionResult DropColumn(string table, string column)
    {
        RequireWriteRole();
        _schema.DropColumn(table, column);
        return NoContent();
    }

    [HttpPost("{table}/fk")]
    public IActionResult AddFk(string table, [FromBody] Dictionary<string, JsonElement> body)
    {
        RequireWriteRole();
        var sql = _schema.AddFk(table, body);
        return StatusCode(201, new { message = "FK angelegt", sql });
    }

    [HttpDelete("{table}/fk/{column}")]
    public IActionResult DropFk(string table, string column)
    {
        RequireWriteRole();
        _schema.DropFk(table, column);
        return NoContent();
    }
}
