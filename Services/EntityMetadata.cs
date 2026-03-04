namespace service.Services;

public record PropertyMetadata(
    string Name,
    string TypeName,
    bool IsPrimaryKey,
    bool IsForeignKey,
    bool IsNullable
);

public record ForeignKeyMetadata(
    string FKPropertyName,
    string NavigationPropertyName,
    string RelatedEntityClrTypeName,
    string RelatedDbSetName,
    string DisplayMember,
    string PrincipalPKName
);

public record EntityMetadata(
    Type ClrType,
    string DbSetName,
    string DisplayName,
    List<PropertyMetadata> Properties,
    List<string> PrimaryKeys,
    List<string> RouteKeys,
    Dictionary<string, ForeignKeyMetadata> ForeignKeys,
    List<string> NavigationIncludes,
    string? SearchExpression,
    string DefaultSort,
    List<string> SortableColumns,
    string Icon = "bi-table",
    string Description = ""
);
