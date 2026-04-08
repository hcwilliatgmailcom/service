using static Service.Helpers.OracleHelpers;

namespace Service.Services;

public class OpenApiService
{
    private readonly MetadataService _metadata;

    public OpenApiService(MetadataService metadata)
    {
        _metadata = metadata;
    }

    public object BuildOpenApi(string baseUrl)
    {
        var paths = new Dictionary<string, object>();
        foreach (var res in _metadata.ListResources())
        {
            var cols = _metadata.GetColumns(res.Name);
            var pk = _metadata.GetPrimaryKey(res.Name);

            var properties = new Dictionary<string, object>();
            foreach (var c in cols)
                properties[c.Name] = new { type = MapJsonType(c.Type) };

            var schema = new { type = "object", properties };

            paths[$"/{res.Name}/"] = new
            {
                get = new { summary = $"Liste {res.Name}", responses = new { _200 = new { description = "OK" } } },
                post = new
                {
                    summary = $"Neuen {res.Name} Datensatz anlegen",
                    requestBody = new { content = new { application_json = new { schema } } },
                    responses = new { _201 = new { description = "Created" } }
                }
            };
            paths[$"/{res.Name}/{{{pk}}}"] = new
            {
                get = new { responses = new { _200 = new { description = "OK" }, _404 = new { description = "Not Found" } } },
                put = new { requestBody = new { content = new { application_json = new { schema } } }, responses = new { _200 = new { description = "OK" } } },
                patch = new { requestBody = new { content = new { application_json = new { schema } } }, responses = new { _200 = new { description = "OK" } } },
                delete_ = new { responses = new { _204 = new { description = "Deleted" } } }
            };
        }

        return new
        {
            openapi = "3.0.0",
            info = new { title = "Service (Oracle)", version = "1.0.0" },
            servers = new[] { new { url = baseUrl } },
            components = new { securitySchemes = new { basic = new { type = "http", scheme = "basic" } } },
            security = new[] { new Dictionary<string, object> { ["basic"] = new object[] { } } },
            paths
        };
    }
}
