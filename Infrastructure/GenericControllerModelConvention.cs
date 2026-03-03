using Microsoft.AspNetCore.Mvc.ApplicationModels;
using service.Controllers;
using service.Services;

namespace service.Infrastructure;

public class GenericControllerModelConvention : IControllerModelConvention
{
    private readonly EntityMetadataService _metadataService;

    public GenericControllerModelConvention(EntityMetadataService metadataService)
    {
        _metadataService = metadataService;
    }

    public void Apply(ControllerModel controller)
    {
        if (!controller.ControllerType.IsGenericType ||
            controller.ControllerType.GetGenericTypeDefinition() != typeof(CrudController<>))
            return;

        var entityType = controller.ControllerType.GenericTypeArguments[0];
        var meta = _metadataService.GetByClrType(entityType);
        if (meta == null) return;

        controller.ControllerName = meta.DbSetName;
    }
}
