using System.Globalization;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using service.Data;
using service.Services;

namespace service.Controllers;

public class CrudController<T> : Controller where T : class
{
    private readonly CmdbContext _context;
    private readonly EntityMetadataService _metadataService;

    public CrudController(CmdbContext context, EntityMetadataService metadataService)
    {
        _context = context;
        _metadataService = metadataService;
    }

    private EntityMetadata Meta => _metadataService.GetByClrType(typeof(T))!;

    private IQueryable<T> DbSet => _context.Set<T>().AsQueryable();

    private IQueryable<T> WithIncludes(IQueryable<T> query)
    {
        foreach (var nav in Meta.NavigationIncludes)
            query = query.Include(nav);
        return query;
    }

    private static readonly HashSet<string> SyncableEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Countries", "Publicholidays"
    };

    private void SetViewMetadata()
    {
        var meta = Meta;
        ViewData["EntityDisplayName"] = meta.DisplayName;
        ViewData["ControllerName"] = meta.DbSetName;
        ViewData["PrimaryKeys"] = meta.PrimaryKeys;
        ViewData["RouteKeys"] = meta.RouteKeys;
        ViewData["SyncAvailable"] = SyncableEntities.Contains(meta.DbSetName);

        var properties = meta.Properties.Select(p => new Dictionary<string, object>
        {
            ["Name"] = p.Name,
            ["Type"] = p.TypeName,
            ["IsPK"] = p.IsPrimaryKey,
            ["IsFK"] = p.IsForeignKey
        }).ToList();
        ViewData["Properties"] = properties;

        var foreignKeys = new Dictionary<string, Dictionary<string, string>>();
        foreach (var (fkPropName, fkMeta) in meta.ForeignKeys)
        {
            foreignKeys[fkPropName] = new Dictionary<string, string>
            {
                ["NavigationProperty"] = fkMeta.NavigationPropertyName,
                ["Controller"] = fkMeta.RelatedDbSetName,
                ["DisplayMember"] = fkMeta.DisplayMember,
                ["FKPropertyName"] = fkMeta.FKPropertyName
            };
        }
        ViewData["ForeignKeys"] = foreignKeys;
    }

    private void SetSortParms(string? sortOrder)
    {
        var meta = Meta;
        ViewData["CurrentSort"] = sortOrder;
        foreach (var col in meta.SortableColumns)
        {
            ViewData[col + "SortParm"] = sortOrder == col ? col + "_desc" : col;
        }
    }

    private IQueryable<T> ApplySort(IQueryable<T> query, string? sortOrder)
    {
        var meta = Meta;

        if (!string.IsNullOrEmpty(sortOrder))
        {
            var desc = sortOrder.EndsWith("_desc");
            var colName = desc ? sortOrder[..^5] : sortOrder;

            // Check if it's a navigation property sort
            string orderExpr;
            if (meta.ForeignKeys.Values.Any(fk => fk.NavigationPropertyName == colName))
            {
                var fk = meta.ForeignKeys.Values.First(fk => fk.NavigationPropertyName == colName);
                orderExpr = $"{fk.NavigationPropertyName}.{fk.DisplayMember}";
            }
            else
            {
                orderExpr = colName;
            }

            orderExpr += desc ? " desc" : " asc";
            return query.OrderBy(orderExpr);
        }

        // Default sort
        return query.OrderBy(meta.DefaultSort);
    }

    public async Task<IActionResult> Index(string searchString, string sortOrder, int? pageNumber)
    {
        ViewData["CurrentFilter"] = searchString;
        SetSortParms(sortOrder);

        var query = WithIncludes(DbSet);

        if (!string.IsNullOrEmpty(searchString) && Meta.SearchExpression != null)
        {
            query = query.Where(Meta.SearchExpression, searchString);
        }

        query = ApplySort(query, sortOrder);

        int pageSize = 10;
        var page = await PaginatedList<T>.CreateAsync(query, pageNumber ?? 1, pageSize);
        ViewData["PageIndex"] = page.PageIndex;
        ViewData["TotalPages"] = page.TotalPages;
        ViewData["HasPreviousPage"] = page.HasPreviousPage;
        ViewData["HasNextPage"] = page.HasNextPage;
        SetViewMetadata();
        return View(page);
    }

    public async Task<IActionResult> Details(
        int? id, int? serverId, int? applicationId, int? contactId,
        int? departmentId, int? networkId, int? databaseInstanceId)
    {
        var keyValues = GetRouteKeyValues(id, serverId, applicationId, contactId, departmentId, networkId, databaseInstanceId);
        if (keyValues == null) return NotFound();

        var entity = await FindEntityAsync(keyValues);
        if (entity == null) return NotFound();

        SetViewMetadata();
        return View(entity);
    }

    public IActionResult Create()
    {
        PopulateFKSelectLists();
        SetViewMetadata();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(T? _placeholder_)
    {
        var entity = Activator.CreateInstance<T>();
        var bindableSet = new HashSet<string>(
            Meta.Properties.Select(p => p.Name));

        // Remove nav property validations
        foreach (var nav in Meta.NavigationIncludes)
            ModelState.Remove(nav);

        if (await TryUpdateModelAsync(entity, "",
                p => p.Name != null && bindableSet.Contains(p.Name)))
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(entity);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError("", ex.InnerException?.Message ?? ex.Message);
                }
            }
        }

        PopulateFKSelectLists(entity);
        await ReloadNavigationProperties(entity);
        SetViewMetadata();
        return View(entity);
    }

    public async Task<IActionResult> Edit(
        int? id, int? serverId, int? applicationId, int? contactId,
        int? departmentId, int? networkId, int? databaseInstanceId)
    {
        var keyValues = GetRouteKeyValues(id, serverId, applicationId, contactId, departmentId, networkId, databaseInstanceId);
        if (keyValues == null) return NotFound();

        var entity = await FindEntityAsync(keyValues);
        if (entity == null) return NotFound();

        PopulateFKSelectLists(entity);
        SetViewMetadata();
        return View(entity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int? id, int? serverId, int? applicationId, int? contactId,
        int? departmentId, int? networkId, int? databaseInstanceId,
        T? _placeholder_)
    {
        var keyValues = GetRouteKeyValues(id, serverId, applicationId, contactId, departmentId, networkId, databaseInstanceId);
        if (keyValues == null) return NotFound();

        var entity = await FindByPKAsync(keyValues);
        if (entity == null) return NotFound();

        var bindableSet = new HashSet<string>(
            Meta.Properties.Select(p => p.Name));

        // Remove nav property validations
        foreach (var nav in Meta.NavigationIncludes)
            ModelState.Remove(nav);

        if (await TryUpdateModelAsync(entity, "",
                p => p.Name != null && bindableSet.Contains(p.Name)))
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(entity);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    var exists = await FindByPKAsync(keyValues) != null;
                    if (!exists) return NotFound();
                    throw;
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError("", ex.InnerException?.Message ?? ex.Message);
                }
            }
        }

        PopulateFKSelectLists(entity);
        await ReloadNavigationProperties(entity);
        SetViewMetadata();
        return View(entity);
    }

    public async Task<IActionResult> Delete(
        int? id, int? serverId, int? applicationId, int? contactId,
        int? departmentId, int? networkId, int? databaseInstanceId)
    {
        var keyValues = GetRouteKeyValues(id, serverId, applicationId, contactId, departmentId, networkId, databaseInstanceId);
        if (keyValues == null) return NotFound();

        var entity = await FindEntityAsync(keyValues);
        if (entity == null) return NotFound();

        SetViewMetadata();
        return View(entity);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int? id, int? serverId, int? applicationId, int? contactId,
        int? departmentId, int? networkId, int? databaseInstanceId)
    {
        var keyValues = GetRouteKeyValues(id, serverId, applicationId, contactId, departmentId, networkId, databaseInstanceId);
        if (keyValues == null) return NotFound();

        var entity = await FindByPKAsync(keyValues);
        if (entity != null) _context.Set<T>().Remove(entity);

        try
        {
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            var routeData = new Dictionary<string, object?>();
            for (int i = 0; i < Meta.RouteKeys.Count; i++)
            {
                routeData[Meta.RouteKeys[i]] = keyValues[i];
            }
            return RedirectToAction(nameof(Delete), routeData);
        }
    }

    public IActionResult Import()
    {
        SetViewMetadata();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select an Excel file to import.";
            SetViewMetadata();
            return View();
        }

        var meta = Meta;
        var propMap = meta.Properties
            .Where(p => !IsCollectionNavigation(p.Name))
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        // Check if the single PK is auto-generated (identity)
        var isIdentityPK = meta.PrimaryKeys.Count == 1 &&
            _context.Model.FindEntityType(typeof(T))!
                .FindProperty(meta.PrimaryKeys[0])!
                .ValueGenerated.HasFlag(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd);

        int inserted = 0, updated = 0, errors = 0;
        var errorMessages = new List<string>();

        // Read Excel rows via MiniExcel (each row is a Dictionary<string, object>)
        using var stream = file.OpenReadStream();
        var rows = stream.Query(useHeaderRow: true).Cast<IDictionary<string, object>>().ToList();

        if (rows.Count == 0)
        {
            TempData["Error"] = "Excel file is empty or has no data rows.";
            SetViewMetadata();
            return View();
        }

        // Map Excel column names to property names (case-insensitive)
        var columnMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in rows[0].Keys)
        {
            var trimmed = key.Trim();
            if (propMap.ContainsKey(trimmed))
                columnMap[key] = trimmed;
        }

        if (columnMap.Count == 0)
        {
            TempData["Error"] = "No matching columns found. Expected: " +
                string.Join(", ", propMap.Keys);
            SetViewMetadata();
            return View();
        }

        int rowNum = 1;
        foreach (var row in rows)
        {
            rowNum++;
            try
            {
                // Extract PK values from the row
                var pkValues = new object?[meta.PrimaryKeys.Count];
                bool hasPK = true;
                for (int i = 0; i < meta.PrimaryKeys.Count; i++)
                {
                    var pkName = meta.PrimaryKeys[i];
                    var matchingCol = columnMap.FirstOrDefault(kv => kv.Value.Equals(pkName, StringComparison.OrdinalIgnoreCase));
                    if (matchingCol.Key != null && row.TryGetValue(matchingCol.Key, out var rawPk) && rawPk != null)
                    {
                        pkValues[i] = ConvertValue(rawPk, typeof(T).GetProperty(pkName)!.PropertyType);
                    }
                    else
                    {
                        hasPK = false;
                        break;
                    }
                }

                T? existing = null;
                if (hasPK && pkValues.All(v => v != null) && !(isIdentityPK && IsDefaultValue(pkValues[0]!)))
                {
                    existing = await _context.Set<T>().FindAsync(pkValues!);
                }

                var entity = existing ?? Activator.CreateInstance<T>();

                // Set property values from Excel row
                foreach (var (excelCol, propName) in columnMap)
                {
                    // Skip PK on identity insert (new records)
                    if (existing == null && isIdentityPK && meta.PrimaryKeys.Contains(propName))
                        continue;

                    var prop = typeof(T).GetProperty(propName);
                    if (prop == null || !prop.CanWrite) continue;

                    row.TryGetValue(excelCol, out var cellValue);
                    var converted = ConvertValue(cellValue, prop.PropertyType);
                    prop.SetValue(entity, converted);
                }

                if (existing != null)
                {
                    _context.Update(entity);
                    updated++;
                }
                else
                {
                    _context.Add(entity);
                    inserted++;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                errors++;
                var msg = ex.InnerException?.Message ?? ex.Message;
                if (errorMessages.Count < 5)
                    errorMessages.Add($"Row {rowNum}: {msg}");
                // Detach any tracked entities that failed
                foreach (var entry in _context.ChangeTracker.Entries().Where(e =>
                    e.State == EntityState.Added || e.State == EntityState.Modified))
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        var summary = $"Import complete: {inserted} inserted, {updated} updated";
        if (errors > 0)
        {
            summary += $", {errors} errors";
            if (errorMessages.Count > 0)
                summary += ". " + string.Join("; ", errorMessages);
        }
        TempData["ImportResult"] = summary;
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Export()
    {
        var meta = Meta;
        var props = meta.Properties.Where(p => !IsCollectionNavigation(p.Name)).ToList();

        var query = WithIncludes(DbSet);
        var items = query.ToDynamicList();

        // Build list of dictionaries for MiniExcel
        var rows = new List<Dictionary<string, object?>>();
        foreach (var item in items)
        {
            var itemObj = (object)item;
            var itemType = itemObj.GetType();
            var row = new Dictionary<string, object?>();
            foreach (var prop in props)
            {
                row[prop.Name] = itemType.GetProperty(prop.Name)?.GetValue(itemObj);
            }
            rows.Add(row);
        }

        var ms = new MemoryStream();
        ms.SaveAs(rows);
        ms.Position = 0;
        return File(ms,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{meta.DbSetName}.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync()
    {
        var meta = Meta;
        if (!SyncableEntities.Contains(meta.DbSetName))
        {
            TempData["Error"] = "Sync is not available for this entity.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            (int added, int updated) result;
            if (meta.DbSetName == "Countries")
            {
                var svc = HttpContext.RequestServices.GetRequiredService<CountrySyncService>();
                result = await svc.SyncAsync(CancellationToken.None);
            }
            else
            {
                var svc = HttpContext.RequestServices.GetRequiredService<HolidaySyncService>();
                result = await svc.SyncAsync(CancellationToken.None);
            }
            TempData["ImportResult"] = $"Sync complete: {result.added} added, {result.updated} updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Sync failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    // --- Helper methods ---

    private static object? ConvertValue(object? raw, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);

        if (raw == null || raw is DBNull)
        {
            if (underlying != null) return null;
            if (targetType == typeof(string)) return null;
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (underlying != null)
            targetType = underlying;

        // If already the right type, return directly
        if (targetType.IsInstanceOfType(raw))
            return raw;

        var str = raw.ToString()?.Trim() ?? "";

        if (targetType == typeof(string)) return str;
        if (string.IsNullOrEmpty(str))
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        if (targetType == typeof(int)) return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(long)) return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(short)) return Convert.ToInt16(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(decimal)) return Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(double)) return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(float)) return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(bool)) return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
        if (targetType == typeof(DateOnly))
        {
            if (raw is DateTime dt) return DateOnly.FromDateTime(dt);
            return DateOnly.Parse(str, CultureInfo.InvariantCulture);
        }
        if (targetType == typeof(DateTime))
        {
            if (raw is DateTime dt) return dt;
            return DateTime.Parse(str, CultureInfo.InvariantCulture);
        }
        if (targetType == typeof(DateTimeOffset))
        {
            if (raw is DateTime dt) return new DateTimeOffset(dt);
            return DateTimeOffset.Parse(str, CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
    }

    private static bool IsDefaultValue(object value)
    {
        if (value is int i) return i == 0;
        if (value is long l) return l == 0;
        if (value is short s) return s == 0;
        return false;
    }

    private object[]? GetRouteKeyValues(
        int? id, int? serverId, int? applicationId, int? contactId,
        int? departmentId, int? networkId, int? databaseInstanceId)
    {
        var meta = Meta;
        if (meta.PrimaryKeys.Count == 1 && meta.RouteKeys[0] == "id")
        {
            return id.HasValue ? new object[] { id.Value } : null;
        }

        var values = new List<object>();
        foreach (var routeKey in meta.RouteKeys)
        {
            int? val = routeKey switch
            {
                "serverId" => serverId,
                "applicationId" => applicationId,
                "contactId" => contactId,
                "departmentId" => departmentId,
                "networkId" => networkId,
                "databaseInstanceId" => databaseInstanceId,
                _ => id
            };
            if (!val.HasValue) return null;
            values.Add(val.Value);
        }
        return values.ToArray();
    }

    private async Task<T?> FindByPKAsync(object[] keyValues)
    {
        return await _context.Set<T>().FindAsync(keyValues);
    }

    private async Task<T?> FindEntityAsync(object[] keyValues)
    {
        // Build a Where expression for composite/single keys
        var meta = Meta;
        var query = WithIncludes(DbSet);

        var conditions = new List<string>();
        var parameters = new List<object>();
        for (int i = 0; i < meta.PrimaryKeys.Count; i++)
        {
            conditions.Add($"{meta.PrimaryKeys[i]} == @{i}");
            parameters.Add(keyValues[i]);
        }
        var where = string.Join(" && ", conditions);
        return await query.Where(where, parameters.ToArray()).FirstOrDefaultAsync();
    }

    private void PopulateFKSelectLists(T? entity = null)
    {
        foreach (var (fkPropName, fkMeta) in Meta.ForeignKeys)
        {
            var relatedType = _context.Model.GetEntityTypes()
                .First(e => e.ClrType.FullName == fkMeta.RelatedEntityClrTypeName).ClrType;

            // Use reflection to get the DbSet
            var dbSetProp = typeof(CmdbContext).GetProperty(fkMeta.RelatedDbSetName);
            if (dbSetProp == null) continue;

            var dbSetValue = dbSetProp.GetValue(_context);
            if (dbSetValue is not IQueryable queryable) continue;

            object? selectedValue = null;
            if (entity != null)
            {
                var prop = typeof(T).GetProperty(fkPropName);
                selectedValue = prop?.GetValue(entity);
            }

            ViewData[fkPropName] = new SelectList(
                queryable.Cast<object>().ToList(),
                fkMeta.PrincipalPKName,
                fkMeta.DisplayMember,
                selectedValue
            );
        }
    }

    private async Task ReloadNavigationProperties(T entity)
    {
        foreach (var (fkPropName, fkMeta) in Meta.ForeignKeys)
        {
            var fkValue = typeof(T).GetProperty(fkPropName)?.GetValue(entity);
            if (fkValue == null) continue;

            var relatedType = _context.Model.GetEntityTypes()
                .First(e => e.ClrType.FullName == fkMeta.RelatedEntityClrTypeName).ClrType;

            var dbSetProp = typeof(CmdbContext).GetProperty(fkMeta.RelatedDbSetName);
            if (dbSetProp == null) continue;

            var related = await _context.FindAsync(relatedType, fkValue);
            if (related != null)
            {
                typeof(T).GetProperty(fkMeta.NavigationPropertyName)?.SetValue(entity, related);
            }
        }
    }

    private bool IsCollectionNavigation(string propertyName)
    {
        var prop = typeof(T).GetProperty(propertyName);
        if (prop == null) return false;
        var type = prop.PropertyType;
        return type.IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>) ||
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }
}
