using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using service.Data;

namespace service.Services;

public class EntityMetadataService
{
    private readonly Dictionary<Type, EntityMetadata> _metadata = new();
    private readonly Dictionary<string, EntityMetadata> _metadataByDbSetName = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<Type, EntityMetadata> All => _metadata;

    public EntityMetadata? GetByClrType(Type type) =>
        _metadata.GetValueOrDefault(type);

    public EntityMetadata? GetByDbSetName(string dbSetName) =>
        _metadataByDbSetName.GetValueOrDefault(dbSetName);

    public IEnumerable<EntityMetadata> GetAllEntities() =>
        _metadata.Values.OrderBy(m => m.DisplayName);

    public void Build(CmdbContext context)
    {
        var model = context.Model;

        // Map CLR types to their DbSet property names
        var dbSetMap = typeof(CmdbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToDictionary(
                p => p.PropertyType.GetGenericArguments()[0],
                p => p.Name
            );

        foreach (var entityType in model.GetEntityTypes())
        {
            var pk = entityType.FindPrimaryKey();
            if (pk == null) continue; // skip view entities

            var clrType = entityType.ClrType;
            if (!dbSetMap.TryGetValue(clrType, out var dbSetName)) continue;

            var pkNames = pk.Properties.Select(p => p.Name).ToList();
            var routeKeys = pkNames.Select(n => pkNames.Count == 1 ? "id" : char.ToLower(n[0]) + n[1..]).ToList();

            var properties = new List<PropertyMetadata>();
            var foreignKeys = new Dictionary<string, ForeignKeyMetadata>();
            var navigationIncludes = new List<string>();
            var sortableColumns = new List<string>();

            // Get all FKs for this entity
            var fks = entityType.GetForeignKeys().ToList();
            var fkPropertyNames = new HashSet<string>(fks.SelectMany(fk => fk.Properties.Select(p => p.Name)));

            foreach (var prop in entityType.GetProperties())
            {
                var isPK = pkNames.Contains(prop.Name);
                var isFK = fkPropertyNames.Contains(prop.Name);
                var clrTypeName = GetFriendlyTypeName(prop.ClrType);
                var isNullable = prop.IsNullable;

                properties.Add(new PropertyMetadata(prop.Name, clrTypeName, isPK, isFK, isNullable));
            }

            // Build FK metadata
            foreach (var fk in fks)
            {
                var navProp = fk.DependentToPrincipal;
                if (navProp == null) continue;

                var principalType = fk.PrincipalEntityType.ClrType;
                if (!dbSetMap.TryGetValue(principalType, out var relatedDbSetName)) continue;

                var principalPK = fk.PrincipalKey.Properties.First().Name;
                var displayMember = FindDisplayMember(fk.PrincipalEntityType);

                foreach (var fkProp in fk.Properties)
                {
                    foreignKeys[fkProp.Name] = new ForeignKeyMetadata(
                        FKPropertyName: fkProp.Name,
                        NavigationPropertyName: navProp.Name,
                        RelatedEntityClrTypeName: principalType.FullName!,
                        RelatedDbSetName: relatedDbSetName,
                        DisplayMember: displayMember,
                        PrincipalPKName: principalPK
                    );
                }

                navigationIncludes.Add(navProp.Name);
            }

            // Build sortable columns (non-PK properties + FK navigation display)
            foreach (var prop in properties)
            {
                if (prop.IsPrimaryKey) continue;
                if (prop.IsForeignKey && foreignKeys.ContainsKey(prop.Name))
                {
                    var fkMeta = foreignKeys[prop.Name];
                    sortableColumns.Add(fkMeta.NavigationPropertyName);
                }
                else
                {
                    sortableColumns.Add(prop.Name);
                }
            }

            // Build search expression
            var searchExpression = BuildSearchExpression(properties, foreignKeys);

            // Default sort: first non-PK string property, or first non-PK property
            var defaultSort = properties
                .Where(p => !p.IsPrimaryKey && !p.IsForeignKey && p.TypeName == "string")
                .Select(p => p.Name)
                .FirstOrDefault()
                ?? properties.Where(p => !p.IsPrimaryKey)
                    .Select(p =>
                    {
                        if (p.IsForeignKey && foreignKeys.ContainsKey(p.Name))
                            return foreignKeys[p.Name].NavigationPropertyName + "." + foreignKeys[p.Name].DisplayMember;
                        return p.Name;
                    })
                    .FirstOrDefault()
                ?? pkNames.First();

            var displayName = SplitPascalCase(clrType.Name);

            var metadata = new EntityMetadata(
                ClrType: clrType,
                DbSetName: dbSetName,
                DisplayName: displayName,
                Properties: properties,
                PrimaryKeys: pkNames,
                RouteKeys: routeKeys,
                ForeignKeys: foreignKeys,
                NavigationIncludes: navigationIncludes,
                SearchExpression: searchExpression,
                DefaultSort: defaultSort,
                SortableColumns: sortableColumns
            );

            _metadata[clrType] = metadata;
            _metadataByDbSetName[dbSetName] = metadata;
        }
    }

    private static string FindDisplayMember(Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType)
    {
        // Prefer "Name", then first string property, then first property
        var props = entityType.GetProperties().ToList();
        var nameProperty = props.FirstOrDefault(p => p.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
        if (nameProperty != null) return nameProperty.Name;

        var stringProp = props.FirstOrDefault(p => p.ClrType == typeof(string));
        if (stringProp != null) return stringProp.Name;

        return props.First().Name;
    }

    private static string? BuildSearchExpression(
        List<PropertyMetadata> properties,
        Dictionary<string, ForeignKeyMetadata> foreignKeys)
    {
        var parts = new List<string>();

        foreach (var prop in properties)
        {
            if (prop.IsPrimaryKey) continue;

            if (prop.IsForeignKey && foreignKeys.ContainsKey(prop.Name))
            {
                var fk = foreignKeys[prop.Name];
                var nav = fk.NavigationPropertyName;
                var dm = fk.DisplayMember;
                parts.Add($"{nav} != null && {nav}.{dm} != null && {nav}.{dm}.Contains(@0)");
            }
            else if (prop.TypeName == "string")
            {
                parts.Add($"{prop.Name} != null && {prop.Name}.Contains(@0)");
            }
        }

        return parts.Count > 0 ? string.Join(" || ", parts) : null;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return GetSimpleTypeName(underlying) + "?";
        }
        return GetSimpleTypeName(type);
    }

    private static string GetSimpleTypeName(Type type)
    {
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(DateOnly)) return "DateOnly";
        if (type == typeof(DateTime)) return "DateTime";
        if (type == typeof(DateTimeOffset)) return "DateTimeOffset";
        if (type == typeof(TimeOnly)) return "TimeOnly";
        return type.Name;
    }

    private static string SplitPascalCase(string input)
    {
        return Regex.Replace(input, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
    }
}
