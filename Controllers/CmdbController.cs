using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Cmdb.Services;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace Cmdb.Controllers;

public class CmdbController : Controller
{
    private readonly SchemaService _schema;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string? _syncBaseUrl;
    private static readonly Dictionary<string, bool> _syncCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _syncLock = new();
    private const int PageSize = 10;

    public CmdbController(SchemaService schema, IHttpClientFactory httpFactory, IConfiguration config)
    {
        _schema = schema;
        _httpFactory = httpFactory;
        _syncBaseUrl = config["SyncBaseUrl"]?.TrimEnd('/');
    }

    // GET /
    [HttpGet("/")]
    public IActionResult Home()
    {
        var entities = _schema.DiscoverEntities()
            .Values.OrderBy(e => e.DisplayName).ToList();
        return View("Home", entities);
    }

    // POST /create_table
    [HttpPost("/create_table")]
    public IActionResult CreateTable()
    {
        var name = Request.Form["table_name"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            TempData["Flash"] = "danger|Table name is required.";
            return Redirect("/");
        }

        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        try
        {
            using var conn = _schema.GetConnection();
            _schema.Execute(conn, $"CREATE TABLE \"{name}\" (ID NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY, NAME VARCHAR2(200))");
            ClearAllCaches();
            TempData["Flash"] = $"success|Table {name} created.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }

        return Redirect("/");
    }

    // GET /entity/{table}
    [HttpGet("/entity/{table}")]
    public async Task<IActionResult> Index(string table, string? search, string? sort, string? dir, int page = 1)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
        {
            TempData["Flash"] = "danger|Unknown entity";
            return RedirectToAction(nameof(Home));
        }

        search = search?.Trim() ?? "";
        dir = dir ?? "asc";
        using var conn = _schema.GetConnection();

        var cols = entity.Columns;
        var colNames = cols.Select(c => c.ColumnName).ToList();
        var pkCols = entity.PkColumns;
        var fkCols = cols.Where(c => c.IsFk).ToList();

        // Build SELECT with FK joins
        var selectParts = colNames.Select(c => $"\"{table.ToUpper()}\".\"{c}\"").ToList();
        var joinParts = new List<string>();
        var fkDisplayMap = new Dictionary<string, (string alias, string displayMember, string navName)>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in fkCols)
        {
            var alias = "FK_" + fk.ColumnName;
            var refTable = fk.FkRefTable!.ToUpper();
            var displayCol = fk.FkDisplayCol ?? "NAME";
            var navName = fk.FkNavName ?? SchemaService.SplitPascal(fk.FkRefTable!);
            selectParts.Add($"\"{alias}\".\"{displayCol}\" AS \"{navName}\"");
            joinParts.Add($"LEFT JOIN \"{refTable}\" \"{alias}\" ON \"{table.ToUpper()}\".\"{fk.ColumnName}\" = \"{alias}\".\"{fk.FkRefPk}\"");
            fkDisplayMap[fk.ColumnName] = (alias, displayCol, navName);
        }

        var baseSql = $"FROM \"{table.ToUpper()}\" {string.Join(" ", joinParts)}";

        // Search
        var where = "";
        var parms = new List<OracleParameter>();
        int paramIdx = 0;
        if (!string.IsNullOrEmpty(search))
        {
            var clauses = new List<string>();
            foreach (var col in cols)
            {
                if (col.DataType.Contains("CHAR", StringComparison.OrdinalIgnoreCase)
                    || col.DataType.Contains("CLOB", StringComparison.OrdinalIgnoreCase))
                {
                    var pName = $":s{paramIdx++}";
                    clauses.Add($"UPPER(\"{table.ToUpper()}\".\"{col.ColumnName}\") LIKE UPPER({pName})");
                    parms.Add(new OracleParameter(pName.TrimStart(':'), $"%{search}%"));
                }
            }
            foreach (var kvp in fkDisplayMap)
            {
                var pName = $":s{paramIdx++}";
                clauses.Add($"UPPER(\"{kvp.Value.alias}\".\"{kvp.Value.displayMember}\") LIKE UPPER({pName})");
                parms.Add(new OracleParameter(pName.TrimStart(':'), $"%{search}%"));
            }
            if (clauses.Count > 0)
                where = "WHERE (" + string.Join(" OR ", clauses) + ")";
        }

        // Count
        var countRow = _schema.QueryOne(conn, $"SELECT COUNT(*) AS CNT {baseSql} {where}", parms.ToArray());
        var total = Convert.ToInt32(countRow?["CNT"] ?? 0);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        // Sort
        var orderSql = "";
        if (!string.IsNullOrEmpty(sort))
        {
            var safeDir = dir == "desc" ? "DESC" : "ASC";
            var fkMatch = fkDisplayMap.Values.FirstOrDefault(f => f.navName.Equals(sort, StringComparison.OrdinalIgnoreCase));
            if (fkMatch != default)
                orderSql = $"ORDER BY \"{fkMatch.alias}\".\"{fkMatch.displayMember}\" {safeDir}";
            else if (colNames.Contains(sort, StringComparer.OrdinalIgnoreCase))
                orderSql = $"ORDER BY \"{table.ToUpper()}\".\"{sort.ToUpper()}\" {safeDir}";
        }
        if (string.IsNullOrEmpty(orderSql) && pkCols.Count > 0)
            orderSql = $"ORDER BY \"{table.ToUpper()}\".\"{pkCols[0]}\" ASC";

        var offset = (page - 1) * PageSize;
        var rowParms = parms.Select(p => new OracleParameter(p.ParameterName, p.Value)).ToList();
        rowParms.Add(new OracleParameter("off", offset));
        rowParms.Add(new OracleParameter("lim", PageSize));
        var rows = _schema.Query(conn,
            $"SELECT {string.Join(", ", selectParts)} {baseSql} {where} {orderSql} OFFSET :off ROWS FETCH NEXT :lim ROWS ONLY",
            rowParms.ToArray());

        // Build display columns
        var displayColumns = new List<DisplayColumn>();
        foreach (var col in cols)
        {
            if (fkDisplayMap.TryGetValue(col.ColumnName, out var fkInfo))
            {
                displayColumns.Add(new DisplayColumn
                {
                    Name = fkInfo.navName,
                    SortKey = fkInfo.navName,
                    IsFk = true,
                    FkCol = col.ColumnName,
                });
            }
            else
            {
                displayColumns.Add(new DisplayColumn
                {
                    Name = SchemaService.SplitPascal(col.ColumnName),
                    SortKey = col.ColumnName,
                    IsFk = false,
                });
            }
        }

        var syncable = await CheckSyncSource(table);

        ViewBag.Entity = entity;
        ViewBag.TableName = table.ToUpper();
        ViewBag.Columns = displayColumns;
        ViewBag.ColNames = colNames;
        ViewBag.Rows = rows;
        ViewBag.PkCols = pkCols;
        ViewBag.FkDisplayMap = fkDisplayMap;
        ViewBag.Search = search;
        ViewBag.Sort = sort ?? "";
        ViewBag.SortDir = dir;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.AllEntities = _schema.DiscoverEntities().Values.OrderBy(e => e.DisplayName).ToList();
        ViewBag.Syncable = syncable;

        return View("Index");
    }

    // GET /entity/{table}/create
    [HttpGet("/entity/{table}/create")]
    public IActionResult Create(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = _schema.GetConnection();
        var fkOptions = LoadFkOptions(conn, entity);

        ViewBag.Entity = entity;
        ViewBag.TableName = table.ToUpper();
        ViewBag.FkOptions = fkOptions;
        return View("Create");
    }

    // POST /entity/{table}/create
    [HttpPost("/entity/{table}/create")]
    public IActionResult CreatePost(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = _schema.GetConnection();
        try
        {
            var cols = entity.Columns;
            var pkCols = entity.PkColumns;
            var fields = new List<string>();
            var placeholders = new List<string>();
            var parms = new List<OracleParameter>();
            int idx = 0;

            foreach (var col in cols)
            {
                if (pkCols.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase) && col.IsIdentity)
                    continue;

                var val = Request.Form[col.ColumnName].FirstOrDefault()?.Trim() ?? "";
                fields.Add($"\"{col.ColumnName}\"");
                if (string.IsNullOrEmpty(val))
                {
                    placeholders.Add("NULL");
                }
                else
                {
                    var pName = $"p{idx++}";
                    placeholders.Add($":{pName}");
                    parms.Add(new OracleParameter(pName, val));
                }
            }

            if (pkCols.Count == 1 && cols.First(c => c.ColumnName == pkCols[0]).IsIdentity)
            {
                var retParm = new OracleParameter("retId", OracleDbType.Decimal, System.Data.ParameterDirection.Output);
                var sql = $"INSERT INTO \"{table.ToUpper()}\" ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)}) RETURNING \"{pkCols[0]}\" INTO :retId";
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                foreach (var p in parms) cmd.Parameters.Add(p);
                cmd.Parameters.Add(retParm);
                cmd.ExecuteNonQuery();
            }
            else
            {
                _schema.Execute(conn, $"INSERT INTO \"{table.ToUpper()}\" ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)})", parms.ToArray());
            }

            TempData["Flash"] = "success|Record created successfully.";
            return Redirect($"/entity/{table.ToUpper()}");
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
            return Redirect($"/entity/{table.ToUpper()}/create");
        }
    }

    // GET /entity/{table}/edit
    [HttpGet("/entity/{table}/edit")]
    public IActionResult Edit(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = _schema.GetConnection();
        var pkCols = entity.PkColumns;
        var pkValues = pkCols.ToDictionary(pk => pk, pk => Request.Query[pk].FirstOrDefault() ?? "");

        var whereParts = pkCols.Select((pk, i) => $"\"{pk}\" = :pk{i}").ToList();
        var whereParms = pkCols.Select((pk, i) => new OracleParameter($"pk{i}", pkValues[pk])).ToArray();
        var row = _schema.QueryOne(conn, $"SELECT * FROM \"{table.ToUpper()}\" WHERE {string.Join(" AND ", whereParts)}", whereParms);

        if (row == null)
        {
            TempData["Flash"] = "danger|Record not found.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        var fkOptions = LoadFkOptions(conn, entity);

        ViewBag.Entity = entity;
        ViewBag.TableName = table.ToUpper();
        ViewBag.Row = row;
        ViewBag.FkOptions = fkOptions;
        return View("Edit");
    }

    // POST /entity/{table}/edit
    [HttpPost("/entity/{table}/edit")]
    public IActionResult EditPost(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = _schema.GetConnection();
        var pkCols = entity.PkColumns;
        var pkValues = pkCols.ToDictionary(pk => pk, pk => Request.Form[pk].FirstOrDefault() ?? "");

        try
        {
            var whereParts = pkCols.Select((pk, i) => $"\"{pk}\" = :pk{i}").ToList();

            var sets = new List<string>();
            var parms = new List<OracleParameter>();
            int idx = 0;

            foreach (var col in entity.Columns)
            {
                if (pkCols.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase))
                    continue;
                var val = Request.Form[col.ColumnName].FirstOrDefault()?.Trim() ?? "";
                if (string.IsNullOrEmpty(val))
                {
                    sets.Add($"\"{col.ColumnName}\" = NULL");
                }
                else
                {
                    var pName = $"u{idx++}";
                    sets.Add($"\"{col.ColumnName}\" = :{pName}");
                    parms.Add(new OracleParameter(pName, val));
                }
            }

            for (int i = 0; i < pkCols.Count; i++)
                parms.Add(new OracleParameter($"pk{i}", pkValues[pkCols[i]]));

            _schema.Execute(conn,
                $"UPDATE \"{table.ToUpper()}\" SET {string.Join(", ", sets)} WHERE {string.Join(" AND ", whereParts)}",
                parms.ToArray());

            TempData["Flash"] = "success|Record updated successfully.";
            return Redirect($"/entity/{table.ToUpper()}");
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
            return Redirect($"/entity/{table.ToUpper()}");
        }
    }

    // GET /entity/{table}/details
    [HttpGet("/entity/{table}/details")]
    public IActionResult Details(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = _schema.GetConnection();
        var pkCols = entity.PkColumns;
        var pkValues = pkCols.ToDictionary(pk => pk, pk => Request.Query[pk].FirstOrDefault() ?? "");
        var cols = entity.Columns;
        var fkCols = cols.Where(c => c.IsFk).ToList();

        var selectParts = cols.Select(c => $"\"{table.ToUpper()}\".\"{c.ColumnName}\"").ToList();
        var joinParts = new List<string>();
        var fkDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in fkCols)
        {
            var alias = "FK_" + fk.ColumnName;
            var refTable = fk.FkRefTable!.ToUpper();
            var displayCol = fk.FkDisplayCol ?? "NAME";
            var navName = fk.FkNavName ?? SchemaService.SplitPascal(fk.FkRefTable!);
            selectParts.Add($"\"{alias}\".\"{displayCol}\" AS \"{navName}\"");
            joinParts.Add($"LEFT JOIN \"{refTable}\" \"{alias}\" ON \"{table.ToUpper()}\".\"{fk.ColumnName}\" = \"{alias}\".\"{fk.FkRefPk}\"");
            fkDisplay[fk.ColumnName] = navName;
        }

        var whereParts = pkCols.Select((pk, i) => $"\"{table.ToUpper()}\".\"{pk}\" = :pk{i}").ToList();
        var whereParms = pkCols.Select((pk, i) => new OracleParameter($"pk{i}", pkValues[pk])).ToArray();

        var row = _schema.QueryOne(conn,
            $"SELECT {string.Join(", ", selectParts)} FROM \"{table.ToUpper()}\" {string.Join(" ", joinParts)} WHERE {string.Join(" AND ", whereParts)}",
            whereParms);

        if (row == null)
        {
            TempData["Flash"] = "danger|Record not found.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        ViewBag.Entity = entity;
        ViewBag.TableName = table.ToUpper();
        ViewBag.Row = row;
        ViewBag.FkDisplay = fkDisplay;
        return View("Details");
    }

    // GET /entity/{table}/delete
    [HttpGet("/entity/{table}/delete")]
    public IActionResult Delete(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = _schema.GetConnection();
        var pkCols = entity.PkColumns;
        var pkValues = pkCols.ToDictionary(pk => pk, pk => Request.Query[pk].FirstOrDefault() ?? "");
        var cols = entity.Columns;
        var fkCols = cols.Where(c => c.IsFk).ToList();

        var selectParts = cols.Select(c => $"\"{table.ToUpper()}\".\"{c.ColumnName}\"").ToList();
        var joinParts = new List<string>();
        var fkDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in fkCols)
        {
            var alias = "FK_" + fk.ColumnName;
            var refTable = fk.FkRefTable!.ToUpper();
            var displayCol = fk.FkDisplayCol ?? "NAME";
            var navName = fk.FkNavName ?? SchemaService.SplitPascal(fk.FkRefTable!);
            selectParts.Add($"\"{alias}\".\"{displayCol}\" AS \"{navName}\"");
            joinParts.Add($"LEFT JOIN \"{refTable}\" \"{alias}\" ON \"{table.ToUpper()}\".\"{fk.ColumnName}\" = \"{alias}\".\"{fk.FkRefPk}\"");
            fkDisplay[fk.ColumnName] = navName;
        }

        var whereParts = pkCols.Select((pk, i) => $"\"{table.ToUpper()}\".\"{pk}\" = :pk{i}").ToList();
        var whereParms = pkCols.Select((pk, i) => new OracleParameter($"pk{i}", pkValues[pk])).ToArray();

        var row = _schema.QueryOne(conn,
            $"SELECT {string.Join(", ", selectParts)} FROM \"{table.ToUpper()}\" {string.Join(" ", joinParts)} WHERE {string.Join(" AND ", whereParts)}",
            whereParms);

        if (row == null)
        {
            TempData["Flash"] = "danger|Record not found.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        ViewBag.Entity = entity;
        ViewBag.TableName = table.ToUpper();
        ViewBag.Row = row;
        ViewBag.FkDisplay = fkDisplay;
        ViewBag.PkValues = pkValues;
        return View("Delete");
    }

    // POST /entity/{table}/delete
    [HttpPost("/entity/{table}/delete")]
    public IActionResult DeletePost(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = _schema.GetConnection();
        var pkCols = entity.PkColumns;
        var pkValues = pkCols.ToDictionary(pk => pk, pk => Request.Form[pk].FirstOrDefault() ?? "");

        try
        {
            var whereParts = pkCols.Select((pk, i) => $"\"{pk}\" = :pk{i}").ToList();
            var whereParms = pkCols.Select((pk, i) => new OracleParameter($"pk{i}", pkValues[pk])).ToArray();
            _schema.Execute(conn, $"DELETE FROM \"{table.ToUpper()}\" WHERE {string.Join(" AND ", whereParts)}", whereParms);

            TempData["Flash"] = "success|Record deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Cannot delete: {ex.Message}";
        }
        return Redirect($"/entity/{table.ToUpper()}");
    }

    // GET /entity/{table}/export
    [HttpGet("/entity/{table}/export")]
    public IActionResult Export(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = _schema.GetConnection();
        var rows = _schema.Query(conn, $"SELECT * FROM \"{table.ToUpper()}\"");

        if (rows.Count == 0)
        {
            TempData["Flash"] = "warning|No data to export.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        var sb = new StringBuilder();
        var headers = rows[0].Keys.ToList();
        sb.AppendLine(string.Join(",", headers.Select(h => CsvEscape(h))));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", headers.Select(h => CsvEscape(row[h]?.ToString() ?? ""))));

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"{table.ToUpper()}.csv");
    }

    // POST /entity/{table}/import
    [HttpPost("/entity/{table}/import")]
    public IActionResult Import(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        var file = Request.Form.Files.FirstOrDefault();
        if (file == null || file.Length == 0)
        {
            TempData["Flash"] = "warning|No file uploaded.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var lines = new List<string>();
            while (!reader.EndOfStream)
                lines.Add(reader.ReadLine() ?? "");

            if (lines.Count < 2)
            {
                TempData["Flash"] = "warning|CSV file is empty.";
                return Redirect($"/entity/{table.ToUpper()}");
            }

            var headers = ParseCsvLine(lines[0]);
            using var conn = _schema.GetConnection();
            int inserted = 0, updated = 0;

            var nameIdx = headers.FindIndex(h => h.Equals("NAME", StringComparison.OrdinalIgnoreCase));

            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var values = ParseCsvLine(lines[i]);

                if (nameIdx >= 0 && nameIdx < values.Count)
                {
                    var nameVal = values[nameIdx];
                    var existing = _schema.QueryOne(conn,
                        $"SELECT * FROM \"{table.ToUpper()}\" WHERE UPPER(\"NAME\") = UPPER(:n)",
                        new OracleParameter("n", nameVal));

                    if (existing != null)
                    {
                        var sets = new List<string>();
                        var parms = new List<OracleParameter>();
                        int pIdx = 0;
                        for (int j = 0; j < headers.Count && j < values.Count; j++)
                        {
                            var h = headers[j].ToUpper();
                            if (h == "ID") continue;
                            if (entity.PkColumns.Contains(h, StringComparer.OrdinalIgnoreCase)) continue;
                            var pName = $"imp{pIdx++}";
                            sets.Add($"\"{h}\" = :{pName}");
                            parms.Add(new OracleParameter(pName, string.IsNullOrEmpty(values[j]) ? DBNull.Value : values[j]));
                        }
                        parms.Add(new OracleParameter("matchName", nameVal));
                        if (sets.Count > 0)
                            _schema.Execute(conn, $"UPDATE \"{table.ToUpper()}\" SET {string.Join(", ", sets)} WHERE UPPER(\"NAME\") = UPPER(:matchName)", parms.ToArray());
                        updated++;
                    }
                    else
                    {
                        InsertCsvRow(conn, table, entity, headers, values);
                        inserted++;
                    }
                }
                else
                {
                    InsertCsvRow(conn, table, entity, headers, values);
                    inserted++;
                }
            }

            TempData["Flash"] = $"success|Import complete: {inserted} inserted, {updated} updated.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Import error: {ex.Message}";
        }

        return Redirect($"/entity/{table.ToUpper()}");
    }

    // POST /entity/{table}/sync
    [HttpPost("/entity/{table}/sync")]
    public async Task<IActionResult> Sync(string table)
    {
        var entities = _schema.DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        if (string.IsNullOrEmpty(_syncBaseUrl))
        {
            TempData["Flash"] = "danger|SyncBaseUrl not configured.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        var url = $"{_syncBaseUrl}/{table.ToLower()}";
        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            var json = await http.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement[]>(json) ?? Array.Empty<JsonElement>();

            using var conn = _schema.GetConnection();
            var cols = entity.Columns;
            var pkCols = entity.PkColumns;
            int inserted = 0, updated = 0;

            foreach (var item in data)
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                // Map JSON properties to column names (normalize: remove underscores, compare case-insensitive)
                var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in item.EnumerateObject())
                {
                    var normalizedProp = prop.Name.Replace("_", "").ToUpper();
                    foreach (var col in cols)
                    {
                        var normalizedCol = col.ColumnName.Replace("_", "").ToUpper();
                        if (normalizedCol == normalizedProp)
                        {
                            var val = prop.Value.ValueKind == JsonValueKind.Null ? ""
                                : prop.Value.ValueKind == JsonValueKind.Array
                                    ? string.Join(", ", prop.Value.EnumerateArray().Select(x => x.ToString()))
                                    : prop.Value.ToString();
                            mapped[col.ColumnName] = val;
                            break;
                        }
                    }
                }

                // Try to find existing row by PK values
                Dictionary<string, object?>? existing = null;
                var hasPkValues = pkCols.All(pk => mapped.ContainsKey(pk) && !string.IsNullOrEmpty(mapped[pk]));

                if (hasPkValues)
                {
                    var whereParts = pkCols.Select((pk, i) => $"\"{pk}\" = :pk{i}").ToList();
                    var whereParms = pkCols.Select((pk, i) => new OracleParameter($"pk{i}", mapped[pk])).ToArray();
                    existing = _schema.QueryOne(conn, $"SELECT * FROM \"{table.ToUpper()}\" WHERE {string.Join(" AND ", whereParts)}", whereParms);
                }
                else if (mapped.ContainsKey("NAME") && !string.IsNullOrEmpty(mapped["NAME"]))
                {
                    existing = _schema.QueryOne(conn, $"SELECT * FROM \"{table.ToUpper()}\" WHERE UPPER(\"NAME\") = UPPER(:n)",
                        new OracleParameter("n", mapped["NAME"]));
                }

                if (existing != null)
                {
                    // Update non-PK columns
                    var sets = new List<string>();
                    var parms = new List<OracleParameter>();
                    int pIdx = 0;
                    foreach (var col in cols)
                    {
                        if (pkCols.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase)) continue;
                        if (col.IsIdentity) continue;
                        if (!mapped.ContainsKey(col.ColumnName)) continue;
                        var pName = $"u{pIdx++}";
                        sets.Add($"\"{col.ColumnName}\" = :{pName}");
                        parms.Add(new OracleParameter(pName, string.IsNullOrEmpty(mapped[col.ColumnName]) ? DBNull.Value : mapped[col.ColumnName]));
                    }
                    if (sets.Count > 0)
                    {
                        // Build WHERE from existing row's PK
                        for (int i = 0; i < pkCols.Count; i++)
                            parms.Add(new OracleParameter($"pk{i}", existing[pkCols[i]]));
                        var wParts = pkCols.Select((pk, i) => $"\"{pk}\" = :pk{i}").ToList();
                        _schema.Execute(conn, $"UPDATE \"{table.ToUpper()}\" SET {string.Join(", ", sets)} WHERE {string.Join(" AND ", wParts)}", parms.ToArray());
                    }
                    updated++;
                }
                else
                {
                    // Insert
                    var fields = new List<string>();
                    var placeholders = new List<string>();
                    var parms = new List<OracleParameter>();
                    int pIdx = 0;
                    foreach (var col in cols)
                    {
                        if (col.IsIdentity) continue;
                        if (!mapped.ContainsKey(col.ColumnName)) continue;
                        fields.Add($"\"{col.ColumnName}\"");
                        if (string.IsNullOrEmpty(mapped[col.ColumnName]))
                        {
                            placeholders.Add("NULL");
                        }
                        else
                        {
                            var pName = $"i{pIdx++}";
                            placeholders.Add($":{pName}");
                            parms.Add(new OracleParameter(pName, mapped[col.ColumnName]));
                        }
                    }
                    if (fields.Count > 0)
                        _schema.Execute(conn, $"INSERT INTO \"{table.ToUpper()}\" ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)})", parms.ToArray());
                    inserted++;
                }
            }

            TempData["Flash"] = $"success|Sync complete: {inserted} inserted, {updated} updated from {url}";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Sync error: {ex.Message}";
        }

        return Redirect($"/entity/{table.ToUpper()}");
    }

    private async Task<bool> CheckSyncSource(string table)
    {
        if (string.IsNullOrEmpty(_syncBaseUrl)) return false;

        lock (_syncLock)
        {
            if (_syncCache.TryGetValue(table, out var cached)) return cached;
        }

        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(3);
            var url = $"{_syncBaseUrl}/{table.ToLower()}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var exists = response.IsSuccessStatusCode;
            lock (_syncLock) { _syncCache[table] = exists; }
            return exists;
        }
        catch
        {
            lock (_syncLock) { _syncCache[table] = false; }
            return false;
        }
    }

    // POST /drop_table/{table}
    [HttpPost("/drop_table/{table}")]
    public IActionResult DropTable(string table)
    {
        try
        {
            using var conn = _schema.GetConnection();
            _schema.Execute(conn, $"DROP TABLE \"{table.ToUpper()}\" CASCADE CONSTRAINTS");
            ClearAllCaches();
            TempData["Flash"] = $"success|Table {table} dropped.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect("/");
    }

    // POST /add_column/{table}
    [HttpPost("/add_column/{table}")]
    public IActionResult AddColumn(string table)
    {
        var colName = Request.Form["column_name"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        var colType = Request.Form["column_type"].FirstOrDefault()?.Trim() ?? "VARCHAR2(200)";

        if (string.IsNullOrEmpty(colName))
        {
            TempData["Flash"] = "danger|Column name is required.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        colName = new string(colName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        try
        {
            using var conn = _schema.GetConnection();
            var oraType = colType.ToUpper() switch
            {
                "NUMBER" => "NUMBER",
                "DATE" => "DATE",
                "TIMESTAMP" => "TIMESTAMP",
                _ => "VARCHAR2(200)"
            };
            _schema.Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" ADD \"{colName}\" {oraType}");
            ClearAllCaches();
            TempData["Flash"] = $"success|Column {colName} added.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect($"/entity/{table.ToUpper()}");
    }

    // POST /drop_column/{table}
    [HttpPost("/drop_column/{table}")]
    public IActionResult DropColumn(string table)
    {
        var colName = Request.Form["column_name"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(colName))
        {
            TempData["Flash"] = "danger|Column name is required.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        try
        {
            using var conn = _schema.GetConnection();
            _schema.Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" DROP COLUMN \"{colName}\"");
            ClearAllCaches();
            TempData["Flash"] = $"success|Column {colName} dropped.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect($"/entity/{table.ToUpper()}");
    }

    // POST /add_reference/{table}
    [HttpPost("/add_reference/{table}")]
    public IActionResult AddReference(string table)
    {
        var refTable = Request.Form["ref_table"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(refTable))
        {
            TempData["Flash"] = "danger|Reference table is required.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        var colName = $"{refTable}_ID";
        try
        {
            using var conn = _schema.GetConnection();
            _schema.Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" ADD \"{colName}\" NUMBER");
            ClearAllCaches();
            TempData["Flash"] = $"success|Reference {colName} added.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect($"/entity/{table.ToUpper()}");
    }

    // POST /drop_reference/{table}
    [HttpPost("/drop_reference/{table}")]
    public IActionResult DropReference(string table)
    {
        var colName = Request.Form["column_name"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(colName))
        {
            TempData["Flash"] = "danger|Column name is required.";
            return Redirect($"/entity/{table.ToUpper()}");
        }

        try
        {
            using var conn = _schema.GetConnection();
            _schema.Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" DROP COLUMN \"{colName}\"");
            ClearAllCaches();
            TempData["Flash"] = $"success|Reference {colName} dropped.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect($"/entity/{table.ToUpper()}");
    }

    private void ClearAllCaches()
    {
        ClearAllCaches();
        lock (_syncLock) { _syncCache.Clear(); }
    }



    // --- Helpers ---

    private Dictionary<string, List<Dictionary<string, object?>>> LoadFkOptions(OracleConnection conn, EntityMeta entity)
    {
        var fkOptions = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in entity.Columns.Where(c => c.IsFk))
        {
            var refTable = col.FkRefTable!.ToUpper();
            var displayCol = col.FkDisplayCol ?? "NAME";
            fkOptions[col.ColumnName] = _schema.Query(conn,
                $"SELECT \"{col.FkRefPk}\" AS PK, \"{displayCol}\" AS DISPLAY FROM \"{refTable}\" ORDER BY \"{displayCol}\"");
        }
        return fkOptions;
    }

    private void InsertCsvRow(OracleConnection conn, string table, EntityMeta entity, List<string> headers, List<string> values)
    {
        var fields = new List<string>();
        var placeholders = new List<string>();
        var parms = new List<OracleParameter>();
        int idx = 0;

        for (int j = 0; j < headers.Count && j < values.Count; j++)
        {
            var h = headers[j].ToUpper();
            if (h == "ID") continue;
            if (entity.PkColumns.Contains(h, StringComparer.OrdinalIgnoreCase)
                && entity.Columns.FirstOrDefault(c => c.ColumnName == h)?.IsIdentity == true)
                continue;

            fields.Add($"\"{h}\"");
            if (string.IsNullOrEmpty(values[j]))
            {
                placeholders.Add("NULL");
            }
            else
            {
                var pName = $"csv{idx++}";
                placeholders.Add($":{pName}");
                parms.Add(new OracleParameter(pName, values[j]));
            }
        }

        if (fields.Count > 0)
            _schema.Execute(conn,
                $"INSERT INTO \"{table.ToUpper()}\" ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)})",
                parms.ToArray());
    }

    private static string CsvEscape(string val)
    {
        if (val.Contains(',') || val.Contains('"') || val.Contains('\n'))
            return "\"" + val.Replace("\"", "\"\"") + "\"";
        return val;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            if (inQuotes)
            {
                if (line[i] == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(line[i]);
                }
            }
            else
            {
                if (line[i] == '"') inQuotes = true;
                else if (line[i] == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(line[i]);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}

public class DisplayColumn
{
    public string Name { get; set; } = "";
    public string SortKey { get; set; } = "";
    public bool IsFk { get; set; }
    public string? FkCol { get; set; }
}
