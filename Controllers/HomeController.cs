using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace Cmdb.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly string? _syncBaseUrl;
    private static readonly Dictionary<string, bool> _syncCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _syncLock = new();
    private static readonly Dictionary<string, (string Email, DateTime Expiry)> _tokens = new();
    private static readonly object _tokenLock = new();
    private const int PageSize = 10;

    // SchemaService fields merged in
    private static Dictionary<string, Cmdb.Services.EntityMeta>? _cache;
    private static readonly object _lock = new();
    private static string? _connStr;

    public HomeController(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
        _syncBaseUrl = config["SyncBaseUrl"]?.TrimEnd('/');
        if (_connStr == null)
            _connStr = config.GetConnectionString("Oracle")
                ?? "User Id=cmdb;Password=cmdb123;Data Source=localhost:1521/XEPDB1;";
    }

    // POST /login
    [HttpPost("/login")]
    public async Task<IActionResult> Login()
    {
        var email = Request.Form["email"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrEmpty(email))
            return Redirect("/");

        var token = Guid.NewGuid().ToString("N");
        lock (_tokenLock) { _tokens[token] = (email, DateTime.UtcNow.AddMinutes(15)); }

        var link = $"{Request.Scheme}://{Request.Host}/auth?token={token}";
        HttpContext.RequestServices.GetRequiredService<ILogger<HomeController>>()
            .LogInformation("Magic link for {Email}: {Link}", email, link);

        if (Request.Host.Host is "localhost" or "127.0.0.1" or "::1")
        {
            TempData["MagicLink"] = link;
        }
        else
        {
            try { await SendMagicLinkEmail(email, link); TempData["Flash"] = "success|Check your email for the login link."; }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<HomeController>>()
                    .LogError(ex, "Failed to send magic link email to {Email}", email);
                TempData["Flash"] = "danger|Could not send email. Contact an administrator.";
            }
        }

        return Redirect("/");
    }

    // GET /auth?token=...
    [HttpGet("/auth")]
    public IActionResult Auth(string token)
    {
        if (string.IsNullOrEmpty(token)) return Redirect("/");

        bool found;
        (string Email, DateTime Expiry) entry;
        lock (_tokenLock)
        {
            found = _tokens.TryGetValue(token, out entry);
            if (found) _tokens.Remove(token);
        }

        if (!found || DateTime.UtcNow > entry.Expiry)
        {
            TempData["Flash"] = "danger|This login link is invalid or has expired.";
            return Redirect("/");
        }

        HttpContext.Session.SetString("email", entry.Email);
        return Redirect("/");
    }

    // GET /logout
    [HttpGet("/logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Redirect("/");
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
            using var conn = GetConnection();
            Execute(conn, $"CREATE TABLE \"{name}\" (ID NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY, NAME VARCHAR2(200))");
            ClearAllCaches();
            return Redirect($"/entity/{name}");
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
            return Redirect("/");
        }
    }

    // GET /entity/{table}
    [HttpGet("/entity/{table}")]
    public async Task<IActionResult> Index(string table, string? search, string? sort, string? dir, int page = 1)
    {
        var entities = DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
        {
            TempData["Flash"] = "danger|Unknown entity";
            return RedirectToAction(nameof(Home));
        }

        search = search?.Trim() ?? "";
        dir = dir ?? "asc";
        using var conn = GetConnection();

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
            var navName = fk.FkNavName ?? SplitPascal(fk.FkRefTable!);
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
        var countRow = QueryOne(conn, $"SELECT COUNT(*) AS CNT {baseSql} {where}", parms.ToArray());
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
        var rows = Query(conn,
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
                    Name = SplitPascal(col.ColumnName),
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
        ViewBag.Syncable = syncable;

        return View("Index");
    }

    // POST /drop_table/{table}
    [HttpPost("/drop_table/{table}")]
    public IActionResult DropTable(string table)
    {
        try
        {
            using var conn = GetConnection();
            Execute(conn, $"DROP TABLE \"{table.ToUpper()}\" CASCADE CONSTRAINTS");
            ClearAllCaches();
            TempData["Flash"] = $"success|Table {table} dropped.";
            return Redirect("/");
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
            return Redirect($"/entity/{table.ToUpper()}");
        }
    }

    // GET /entity/{table}/export
    [HttpGet("/entity/{table}/export")]
    public IActionResult Export(string table)
    {
        var entities = DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = GetConnection();
        var rows = Query(conn, $"SELECT * FROM \"{table.ToUpper()}\"");

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
        var entities = DiscoverEntities();
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
            using var conn = GetConnection();
            int inserted = 0, updated = 0;

            var nameIdx = headers.FindIndex(h => h.Equals("NAME", StringComparison.OrdinalIgnoreCase));

            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var values = ParseCsvLine(lines[i]);

                if (nameIdx >= 0 && nameIdx < values.Count)
                {
                    var nameVal = values[nameIdx];
                    var existing = QueryOne(conn,
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
                            var colMeta = entity.Columns.FirstOrDefault(c => c.ColumnName.Equals(h, StringComparison.OrdinalIgnoreCase));
                            if (string.IsNullOrEmpty(values[j]))
                                parms.Add(new OracleParameter(pName, DBNull.Value));
                            else if (colMeta != null)
                                parms.Add(MakeTypedParam(pName, values[j], colMeta));
                            else
                                parms.Add(new OracleParameter(pName, values[j]));
                        }
                        parms.Add(new OracleParameter("matchName", nameVal));
                        if (sets.Count > 0)
                            Execute(conn, $"UPDATE \"{table.ToUpper()}\" SET {string.Join(", ", sets)} WHERE UPPER(\"NAME\") = UPPER(:matchName)", parms.ToArray());
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
        var entities = DiscoverEntities();
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

            using var conn = GetConnection();
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
                    existing = QueryOne(conn, $"SELECT * FROM \"{table.ToUpper()}\" WHERE {string.Join(" AND ", whereParts)}", whereParms);
                }
                else if (mapped.ContainsKey("NAME") && !string.IsNullOrEmpty(mapped["NAME"]))
                {
                    existing = QueryOne(conn, $"SELECT * FROM \"{table.ToUpper()}\" WHERE UPPER(\"NAME\") = UPPER(:n)",
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
                        if (string.IsNullOrEmpty(mapped[col.ColumnName]))
                            parms.Add(new OracleParameter(pName, DBNull.Value));
                        else
                            parms.Add(MakeTypedParam(pName, mapped[col.ColumnName], col));
                    }
                    if (sets.Count > 0)
                    {
                        // Build WHERE from existing row's PK
                        for (int i = 0; i < pkCols.Count; i++)
                            parms.Add(new OracleParameter($"pk{i}", existing[pkCols[i]]));
                        var wParts = pkCols.Select((pk, i) => $"\"{pk}\" = :pk{i}").ToList();
                        Execute(conn, $"UPDATE \"{table.ToUpper()}\" SET {string.Join(", ", sets)} WHERE {string.Join(" AND ", wParts)}", parms.ToArray());
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
                            parms.Add(MakeTypedParam(pName, mapped[col.ColumnName], col));
                        }
                    }
                    if (fields.Count > 0)
                        Execute(conn, $"INSERT INTO \"{table.ToUpper()}\" ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)})", parms.ToArray());
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
            if (exists) lock (_syncLock) { _syncCache[table] = true; }
            return exists;
        }
        catch
        {
            return false;
        }
    }

    // POST /entity/{table}/create — inserts empty record, opens edit page
    [HttpPost("/entity/{table}/create")]
    public IActionResult Create(string table)
    {
        var entities = DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = GetConnection();
        try
        {
            var pkCols = entity.PkColumns;
            var nonIdentityCols = entity.Columns
                .Where(c => !(pkCols.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase) && c.IsIdentity))
                .ToList();

            var fields = nonIdentityCols.Select(c => $"\"{c.ColumnName}\"").ToList();
            var placeholders = nonIdentityCols.Select(_ => "NULL").ToList();

            if (pkCols.Count == 1 && entity.Columns.First(c => c.ColumnName == pkCols[0]).IsIdentity)
            {
                var retParm = new OracleParameter("retId", OracleDbType.Decimal, System.Data.ParameterDirection.Output);
                var sql = fields.Count > 0
                    ? $"INSERT INTO \"{table.ToUpper()}\" ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)}) RETURNING \"{pkCols[0]}\" INTO :retId"
                    : $"INSERT INTO \"{table.ToUpper()}\" VALUES (DEFAULT) RETURNING \"{pkCols[0]}\" INTO :retId";
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(retParm);
                cmd.ExecuteNonQuery();
                var newId = retParm.Value?.ToString() ?? "";
                return Redirect($"/entity/{table.ToUpper()}/edit?{pkCols[0]}={Uri.EscapeDataString(newId)}");
            }
            else
            {
                Execute(conn, $"INSERT INTO \"{table.ToUpper()}\" ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)})");
                return Redirect($"/entity/{table.ToUpper()}");
            }
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
            return Redirect($"/entity/{table.ToUpper()}");
        }
    }

    // GET /entity/{table}/edit
    [HttpGet("/entity/{table}/edit")]
    public IActionResult Edit(string table)
    {
        var entities = DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = GetConnection();
        var pkCols = entity.PkColumns;
        var pkValues = pkCols.ToDictionary(pk => pk, pk => Request.Query[pk].FirstOrDefault() ?? "");

        var whereParts = pkCols.Select((pk, i) => $"\"{pk}\" = :pk{i}").ToList();
        var whereParms = pkCols.Select((pk, i) => new OracleParameter($"pk{i}", pkValues[pk])).ToArray();
        var row = QueryOne(conn, $"SELECT * FROM \"{table.ToUpper()}\" WHERE {string.Join(" AND ", whereParts)}", whereParms);

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
        var entities = DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = GetConnection();
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
                    parms.Add(MakeTypedParam(pName, val, col));
                }
            }

            for (int i = 0; i < pkCols.Count; i++)
                parms.Add(new OracleParameter($"pk{i}", pkValues[pkCols[i]]));

            Execute(conn,
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

    // POST /entity/{table}/delete
    [HttpPost("/entity/{table}/delete")]
    public IActionResult DeletePost(string table)
    {
        var entities = DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = GetConnection();
        var pkCols = entity.PkColumns;
        var pkValues = pkCols.ToDictionary(pk => pk, pk => Request.Form[pk].FirstOrDefault() ?? "");

        try
        {
            var whereParts = pkCols.Select((pk, i) => $"\"{pk}\" = :pk{i}").ToList();
            var whereParms = pkCols.Select((pk, i) => new OracleParameter($"pk{i}", pkValues[pk])).ToArray();
            Execute(conn, $"DELETE FROM \"{table.ToUpper()}\" WHERE {string.Join(" AND ", whereParts)}", whereParms);

            TempData["Flash"] = "success|Record deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Cannot delete: {ex.Message}";
        }
        return Redirect($"/entity/{table.ToUpper()}");
    }

    // GET /entity/{table}/details
    [HttpGet("/entity/{table}/details")]
    public IActionResult Details(string table)
    {
        var entities = DiscoverEntities();
        if (!entities.TryGetValue(table, out var entity))
            return Redirect("/");

        using var conn = GetConnection();
        var pkCols = entity.PkColumns;
        var pkValues = pkCols.ToDictionary(pk => pk, pk => Request.Query[pk].FirstOrDefault() ?? "");
        var cols = entity.Columns;
        var fkCols = cols.Where(c => c.IsFk).ToList();

        var selectParts = cols.Select(c => $"\"{table.ToUpper()}\".\"{c.ColumnName}\"").ToList();
        var joinParts = new List<string>();
        var fkDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fkLinks = new Dictionary<string, (string refTable, string refPk, bool isView)>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in fkCols)
        {
            var alias = "FK_" + fk.ColumnName;
            var refTable = fk.FkRefTable!.ToUpper();
            var displayCol = fk.FkDisplayCol ?? "NAME";
            var navName = fk.FkNavName ?? SplitPascal(fk.FkRefTable!);
            selectParts.Add($"\"{alias}\".\"{displayCol}\" AS \"{navName}\"");
            joinParts.Add($"LEFT JOIN \"{refTable}\" \"{alias}\" ON \"{table.ToUpper()}\".\"{fk.ColumnName}\" = \"{alias}\".\"{fk.FkRefPk}\"");
            fkDisplay[fk.ColumnName] = navName;
            var isView = entities.TryGetValue(refTable, out var refEntity) && refEntity.IsView;
            fkLinks[fk.ColumnName] = (refTable, fk.FkRefPk ?? "ID", isView);
        }

        var whereParts = pkCols.Select((pk, i) => $"\"{table.ToUpper()}\".\"{pk}\" = :pk{i}").ToList();
        var whereParms = pkCols.Select((pk, i) => new OracleParameter($"pk{i}", pkValues[pk])).ToArray();

        var row = QueryOne(conn,
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
        ViewBag.FkLinks = fkLinks;
        return View("Details");
    }

    // POST /add_column/{table}
    [HttpPost("/add_column/{table}")]
    public IActionResult AddColumn(string table)
    {
        var colName = Request.Form["column_name"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        var colType = Request.Form["column_type"].FirstOrDefault()?.Trim() ?? "VARCHAR2(200)";
        var refTable = Request.Form["ref_table"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        var nullable = Request.Form["nullable"].FirstOrDefault() == "on";
        var returnUrl = Request.Form["return_url"].FirstOrDefault() ?? "/schema";

        // FK reference mode
        if (!string.IsNullOrEmpty(refTable))
        {
            var fkCol = $"{refTable}_ID";
            try
            {
                using var conn = GetConnection();
                Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" ADD \"{fkCol}\" NUMBER");
                ClearAllCaches();
                TempData["Flash"] = $"success|Reference {fkCol} added.";
            }
            catch (Oracle.ManagedDataAccess.Client.OracleException ex) when (ex.Number == 1430)
            {
                TempData["Flash"] = $"danger|Column {fkCol} already exists in {table.ToUpper()}.";
            }
            catch (Exception ex)
            {
                TempData["Flash"] = $"danger|Error: {ex.Message}";
            }
            return Redirect(returnUrl);
        }

        if (string.IsNullOrEmpty(colName))
        {
            TempData["Flash"] = "danger|Column name is required.";
            return Redirect(returnUrl);
        }

        colName = new string(colName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        try
        {
            using var conn = GetConnection();
            var oraType = colType.ToUpper() switch
            {
                "NUMBER" => "NUMBER",
                "DATE" => "DATE",
                "TIMESTAMP" => "TIMESTAMP",
                _ => "VARCHAR2(200)"
            };
            var nullClause = nullable ? "" : " NOT NULL";
            Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" ADD \"{colName}\" {oraType}{nullClause}");
            ClearAllCaches();
            TempData["Flash"] = $"success|Column {colName} added.";
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex) when (ex.Number == 1430)
        {
            TempData["Flash"] = $"danger|Column {colName} already exists in {table.ToUpper()}.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect(returnUrl);
    }

    // GET /schema
    [HttpGet("/schema")]
    public IActionResult Schema()
    {
        var entities = DiscoverEntities()
            .Values.Where(e => !e.IsView).OrderBy(e => e.DisplayName).ToList();
        ViewBag.AllEntities = entities;
        return View("Schema", entities);
    }

    // POST /drop_column/{table}
    [HttpPost("/drop_column/{table}")]
    public IActionResult DropColumn(string table)
    {
        var colName = Request.Form["column_name"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(colName))
        {
            TempData["Flash"] = "danger|Column name is required.";
            return Redirect("/schema");
        }
        if (colName.Equals("NAME", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Flash"] = "danger|The NAME column cannot be dropped.";
            return Redirect("/schema");
        }

        try
        {
            using var conn = GetConnection();
            Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" DROP COLUMN \"{colName}\"");
            ClearAllCaches();
            TempData["Flash"] = $"success|Column {colName} dropped.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect("/schema");
    }

    // POST /add_reference/{table}
    [HttpPost("/add_reference/{table}")]
    public IActionResult AddReference(string table)
    {
        var refTable = Request.Form["ref_table"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(refTable))
        {
            TempData["Flash"] = "danger|Reference table is required.";
            return Redirect("/schema");
        }

        var colName = $"{refTable}_ID";
        try
        {
            using var conn = GetConnection();
            Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" ADD \"{colName}\" NUMBER");
            ClearAllCaches();
            TempData["Flash"] = $"success|Reference {colName} added.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect("/schema");
    }

    // POST /drop_reference/{table}
    [HttpPost("/drop_reference/{table}")]
    public IActionResult DropReference(string table)
    {
        var colName = Request.Form["column_name"].FirstOrDefault()?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(colName))
        {
            TempData["Flash"] = "danger|Column name is required.";
            return Redirect("/schema");
        }

        try
        {
            using var conn = GetConnection();
            Execute(conn, $"ALTER TABLE \"{table.ToUpper()}\" DROP COLUMN \"{colName}\"");
            ClearAllCaches();
            TempData["Flash"] = $"success|Reference {colName} dropped.";
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error: {ex.Message}";
        }
        return Redirect("/schema");
    }

    // GET /
    [HttpGet("/")]
    public IActionResult Home()
    {
        var email = HttpContext.Session.GetString("email");
        ViewBag.UserEmail = email;
        if (email == null)
            return View("Home", new List<Cmdb.Services.EntityMeta>());

        try
        {
            ClearCache();
            var entities = DiscoverEntities()
                .Values.OrderBy(e => e.DisplayName).ToList();
            return View("Home", entities);
        }
        catch (Exception ex)
        {
            TempData["Flash"] = $"danger|Error loading entities: {ex.Message}";
            return View("Home", new List<Cmdb.Services.EntityMeta>());
        }
    }

    // --- Helpers ---

    private OracleConnection GetConnection()
    {
        var builder = new OracleConnectionStringBuilder(_connStr ?? "User Id=cmdb;Password=cmdb123;Data Source=localhost:1521/XEPDB1;")
        {
            StatementCacheSize = 0
        };
        var conn = new OracleConnection(builder.ToString());
        conn.Open();
        return conn;
    }

    private void ClearCache()
    {
        lock (_lock) { _cache = null; }
    }

    private void ClearAllCaches()
    {
        ClearCache();
        lock (_syncLock) { _syncCache.Clear(); }
    }

    private Dictionary<string, Cmdb.Services.EntityMeta> DiscoverEntities()
    {
        lock (_lock)
        {
            if (_cache != null) return _cache;
        }

        var entities = new Dictionary<string, Cmdb.Services.EntityMeta>(StringComparer.OrdinalIgnoreCase);
        using var conn = GetConnection();

        var tables = Query(conn, @"
            SELECT t.TABLE_NAME, c.COMMENTS, 'TABLE' AS OBJECT_TYPE
            FROM USER_TABLES t
            LEFT JOIN USER_TAB_COMMENTS c ON c.TABLE_NAME = t.TABLE_NAME
            UNION ALL
            SELECT v.VIEW_NAME, c.COMMENTS, 'VIEW' AS OBJECT_TYPE
            FROM USER_VIEWS v
            LEFT JOIN USER_TAB_COMMENTS c ON c.TABLE_NAME = v.VIEW_NAME
            ORDER BY TABLE_NAME");

        foreach (var tbl in tables)
        {
            var tableName = tbl["TABLE_NAME"]?.ToString() ?? "";
            var comment = tbl["COMMENTS"]?.ToString() ?? "";
            var isView = tbl["OBJECT_TYPE"]?.ToString() == "VIEW";
            string icon = isView ? "bi-eye" : "bi-table", desc = tableName;

            if (!string.IsNullOrEmpty(comment) && comment.Contains('|'))
            {
                var parts = comment.Split('|', 2);
                icon = parts[0].Trim();
                desc = parts[1].Trim();
            }

            var meta = new Cmdb.Services.EntityMeta
            {
                TableName = tableName,
                Icon = icon,
                Description = desc,
                DisplayName = SplitPascal(tableName),
                IsView = isView,
            };

            meta.Columns = GetColumns(conn, tableName);
            meta.PkColumns = GetPkColumns(conn, tableName);

            // Views have no PK constraints — use ID column as pseudo-PK if available
            if (isView && meta.PkColumns.Count == 0)
            {
                var idCol = meta.Columns.FirstOrDefault(c =>
                    c.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase));
                if (idCol != null)
                    meta.PkColumns.Add(idCol.ColumnName);
            }

            foreach (var col in meta.Columns)
            {
                if (col.ColumnName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase)
                    && !meta.PkColumns.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    var refTable = col.ColumnName[..^3];
                    col.IsFk = true;
                    col.FkRefTable = refTable;
                    col.FkRefPk = "ID";
                    col.FkDisplayCol = GetDisplayColumn(conn, refTable);
                    col.FkNavName = SplitPascal(refTable);
                }
            }

            if (meta.PkColumns.Count > 1)
                meta.IsCompositePk = true;

            meta.DisplayColumn = GetDisplayColumn(conn, tableName);

            entities[tableName] = meta;
        }

        lock (_lock) { _cache = entities; }
        return entities;
    }

    private List<Cmdb.Services.ColumnMeta> GetColumns(OracleConnection conn, string tableName)
    {
        var cols = new List<Cmdb.Services.ColumnMeta>();
        var rows = Query(conn,
            "SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, DATA_PRECISION, DATA_SCALE, NULLABLE, DATA_DEFAULT, COLUMN_ID FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :t ORDER BY COLUMN_ID",
            new OracleParameter("t", tableName.ToUpper()));

        foreach (var r in rows)
        {
            var colName = r["COLUMN_NAME"]?.ToString() ?? "";
            var dataType = r["DATA_TYPE"]?.ToString() ?? "";
            var nullable = r["NULLABLE"]?.ToString() == "Y";
            var dataDefault = r["DATA_DEFAULT"]?.ToString()?.Trim() ?? "";
            bool isIdentity = dataDefault.Contains("ISEQ$$") || dataDefault.Contains("GENERATED");

            cols.Add(new Cmdb.Services.ColumnMeta
            {
                ColumnName = colName,
                DataType = dataType,
                IsNullable = nullable,
                IsIdentity = isIdentity,
            });
        }

        try
        {
            var identRows = Query(conn,
                "SELECT COLUMN_NAME FROM USER_TAB_IDENTITY_COLS WHERE TABLE_NAME = :t",
                new OracleParameter("t", tableName.ToUpper()));
            foreach (var ir in identRows)
            {
                var idCol = ir["COLUMN_NAME"]?.ToString() ?? "";
                var col = cols.FirstOrDefault(c => c.ColumnName.Equals(idCol, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.IsIdentity = true;
            }
        }
        catch { }

        return cols;
    }

    private List<string> GetPkColumns(OracleConnection conn, string tableName)
    {
        var pks = new List<string>();
        var rows = Query(conn, @"
            SELECT cc.COLUMN_NAME
            FROM USER_CONSTRAINTS c
            JOIN USER_CONS_COLUMNS cc ON cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
            WHERE c.TABLE_NAME = :t AND c.CONSTRAINT_TYPE = 'P'
            ORDER BY cc.POSITION",
            new OracleParameter("t", tableName.ToUpper()));

        foreach (var r in rows)
            pks.Add(r["COLUMN_NAME"]?.ToString() ?? "");

        return pks;
    }

    private string GetDisplayColumn(OracleConnection conn, string tableName)
    {
        var cols = Query(conn,
            "SELECT COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :t ORDER BY COLUMN_ID",
            new OracleParameter("t", tableName.ToUpper()));

        var nameCol = cols.FirstOrDefault(c => c["COLUMN_NAME"]?.ToString()?.Equals("NAME", StringComparison.OrdinalIgnoreCase) == true);
        if (nameCol != null) return nameCol["COLUMN_NAME"]?.ToString() ?? "NAME";

        var varcharCol = cols.FirstOrDefault(c => c["DATA_TYPE"]?.ToString()?.Contains("CHAR") == true);
        if (varcharCol != null) return varcharCol["COLUMN_NAME"]?.ToString() ?? "";

        return cols.FirstOrDefault()?["COLUMN_NAME"]?.ToString() ?? "ID";
    }

    private List<Dictionary<string, object?>> Query(OracleConnection conn, string sql, params OracleParameter[] parms)
    {
        try
        {
            return RunQuery(conn, sql, parms);
        }
        catch (OracleException ex) when (ex.Number == 24449)
        {
            conn.PurgeStatementCache();
            return RunQuery(conn, sql, parms);
        }
        catch (OracleException ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    private Dictionary<string, object?>? QueryOne(OracleConnection conn, string sql, params OracleParameter[] parms)
    {
        var rows = Query(conn, sql, parms);
        return rows.Count > 0 ? rows[0] : null;
    }

    private int Execute(OracleConnection conn, string sql, params OracleParameter[] parms)
    {
        try
        {
            return RunExecute(conn, sql, parms);
        }
        catch (OracleException ex) when (ex.Number == 24449)
        {
            conn.PurgeStatementCache();
            return RunExecute(conn, sql, parms);
        }
        catch (OracleException ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    private static string SplitPascal(string name)
    {
        name = name.Replace("_", " ");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", " ");
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
    }

    private static List<Dictionary<string, object?>> RunQuery(OracleConnection conn, string sql, OracleParameter[] parms)
    {
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        foreach (var p in parms) cmd.Parameters.Add(new OracleParameter(p.ParameterName, p.Value));
        using var rdr = cmd.ExecuteReader();
        var results = new List<Dictionary<string, object?>>();
        while (rdr.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rdr.FieldCount; i++)
                row[rdr.GetName(i)] = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
            results.Add(row);
        }
        return results;
    }

    private static int RunExecute(OracleConnection conn, string sql, OracleParameter[] parms)
    {
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        foreach (var p in parms) cmd.Parameters.Add(new OracleParameter(p.ParameterName, p.Value));
        return cmd.ExecuteNonQuery();
    }

    private Dictionary<string, List<Dictionary<string, object?>>> LoadFkOptions(OracleConnection conn, Cmdb.Services.EntityMeta entity)
    {
        var fkOptions = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in entity.Columns.Where(c => c.IsFk))
        {
            var refTable = col.FkRefTable!.ToUpper();
            var displayCol = col.FkDisplayCol ?? "NAME";
            fkOptions[col.ColumnName] = Query(conn,
                $"SELECT \"{col.FkRefPk}\" AS PK, \"{displayCol}\" AS DISPLAY FROM \"{refTable}\" ORDER BY \"{displayCol}\"");
        }
        return fkOptions;
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

    private static OracleParameter MakeTypedParam(string name, string val, Cmdb.Services.ColumnMeta col)
    {
        var dt = col.DataType.ToUpper();
        if (dt.Contains("DATE") || dt.Contains("TIMESTAMP"))
        {
            if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return new OracleParameter(name, OracleDbType.Date) { Value = d };
        }
        return new OracleParameter(name, val);
    }

    private void InsertCsvRow(OracleConnection conn, string table, Cmdb.Services.EntityMeta entity, List<string> headers, List<string> values)
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
                var colMeta = entity.Columns.FirstOrDefault(c => c.ColumnName.Equals(h, StringComparison.OrdinalIgnoreCase));
                if (colMeta != null)
                    parms.Add(MakeTypedParam(pName, values[j], colMeta));
                else
                    parms.Add(new OracleParameter(pName, values[j]));
            }
        }

        if (fields.Count > 0)
            Execute(conn,
                $"INSERT INTO \"{table.ToUpper()}\" ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)})",
                parms.ToArray());
    }

    private static string CsvEscape(string val)
    {
        if (val.Contains(',') || val.Contains('"') || val.Contains('\n'))
            return "\"" + val.Replace("\"", "\"\"") + "\"";
        return val;
    }

    private async Task SendMagicLinkEmail(string to, string link)
    {
        var smtp = _config.GetSection("Smtp");
        var host = smtp["Host"] ?? throw new InvalidOperationException("Smtp:Host not configured");
        var port = smtp.GetValue("Port", 587);
        var user = smtp["User"];
        var pass = smtp["Password"];
        var from = smtp["From"] ?? user ?? "no-reply@localhost";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = !string.IsNullOrEmpty(user) ? new NetworkCredential(user, pass) : null
        };

        using var msg = new MailMessage(from, to)
        {
            Subject = "Your CMDB login link",
            Body = $"<p>Click the link below to sign in to CMDB. This link expires in 15 minutes.</p><p><a href=\"{link}\">{link}</a></p>",
            IsBodyHtml = true,
        };

        await client.SendMailAsync(msg);
    }
}

public class DisplayColumn
{
    public string Name { get; set; } = "";
    public string SortKey { get; set; } = "";
    public bool IsFk { get; set; }
    public string? FkCol { get; set; }
}
