using Microsoft.AspNetCore.Mvc;
using Service.Services;

namespace Service.Controllers;

[Route("api")]
public class MetadataController : BaseApiController
{
    private readonly MetadataService _metadata;
    private readonly OpenApiService _openApi;

    public MetadataController(AuthorizationService authService, MetadataService metadata, OpenApiService openApi)
        : base(authService)
    {
        _metadata = metadata;
        _openApi = openApi;
    }

    [HttpGet("_openapi")]
    public IActionResult GetOpenApi()
    {
        return Ok(_openApi.BuildOpenApi(BaseUrl));
    }

    [HttpGet("_metadata/{table}")]
    public IActionResult GetMetadata(string table)
    {
        var resolved = _metadata.ResolveTable(table);
        return Ok(new
        {
            name = resolved,
            columns = _metadata.GetColumns(resolved),
            primary_key = _metadata.GetPrimaryKey(resolved),
            is_view = _metadata.IsView(resolved),
            foreign_keys = _metadata.GetForeignKeys(resolved).Select(fk => new
            {
                column = fk.Column,
                ref_table = fk.RefTable,
                ref_column = fk.RefColumn
            })
        });
    }

    [HttpGet("_fk/{table}/{column}")]
    public IActionResult GetFkValues(string table, string column)
    {
        var resolved = _metadata.ResolveTable(table);
        return Ok(_metadata.GetFkValues(resolved, column));
    }
}
