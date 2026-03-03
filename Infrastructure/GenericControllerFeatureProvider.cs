using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using service.Controllers;
using service.Services;

namespace service.Infrastructure;

public class GenericControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly EntityMetadataService _metadataService;

    public GenericControllerFeatureProvider(EntityMetadataService metadataService)
    {
        _metadataService = metadataService;
    }

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var meta in _metadataService.GetAllEntities())
        {
            var controllerType = typeof(CrudController<>).MakeGenericType(meta.ClrType).GetTypeInfo();
            if (!feature.Controllers.Contains(controllerType))
            {
                feature.Controllers.Add(controllerType);
            }
        }
    }
}
