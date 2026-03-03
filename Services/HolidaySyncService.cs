using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using service.Data;
using service.Models;

namespace service.Services;

public class HolidaySyncService
{
    private const int BatchSize = 500;
    private const int MaxParallelFetches = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HolidaySyncService> _logger;

    public HolidaySyncService(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<HolidaySyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public record HolidayDto(string CountryCode, DateTime Date, string Name, string? LocalName, string? Type);

    public async Task<(int Added, int Updated)> SyncAsync(CancellationToken ct)
    {
        try
        {
            List<string> countryCodes;
            using (var db = CreateDbContext())
            {
                countryCodes = await db.Countries
                    .AsNoTracking()
                    .Select(c => c.Code)
                    .ToListAsync(ct);
            }

            var data = await FetchAsync(countryCodes, ct);
            return await UpsertAsync(data, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Holiday sync failed.");
            return (0, 0);
        }
    }

    public async Task<List<HolidayDto>> FetchAsync(List<string> countryCodes, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var year = DateTime.Now.Year;
        var semaphore = new SemaphoreSlim(MaxParallelFetches);
        var results = new List<HolidayDto>();
        var lockObj = new object();

        var tasks = countryCodes.Select(async code =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var url = $"https://date.nager.at/api/v3/PublicHolidays/{year}/{code}";
                var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                var batch = new List<HolidayDto>();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var dateStr = item.TryGetProperty("date", out var d) ? d.GetString() : null;
                    if (dateStr == null || !DateTime.TryParse(dateStr, out var date)) continue;

                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var localName = item.TryGetProperty("localName", out var ln) ? ln.GetString() : null;
                    var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;

                    batch.Add(new HolidayDto(code, date, name, localName, type));
                }

                lock (lockObj)
                {
                    results.AddRange(batch);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch holidays for {Code}.", code);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        _logger.LogInformation("Fetched {Count} holidays from API.", results.Count);
        return results;
    }

    public async Task<(int Added, int Updated)> UpsertAsync(List<HolidayDto> data, CancellationToken ct)
    {
        Dictionary<string, decimal> countryByCode;
        HashSet<(decimal CountryId, DateTime Date, string Name)> existingKeys;

        using (var readDb = CreateDbContext())
        {
            countryByCode = await readDb.Countries
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Code, c => c.Id, ct);

            existingKeys = (await readDb.Publicholidays
                .AsNoTracking()
                .Select(h => new { h.CountryId, h.Date, h.Name })
                .ToListAsync(ct))
                .Select(h => (h.CountryId, h.Date, h.Name))
                .ToHashSet();
        }

        var resolved = data
            .Where(d => countryByCode.ContainsKey(d.CountryCode))
            .Select(d => (Dto: d, CountryId: countryByCode[d.CountryCode]))
            .ToList();

        var toInsert = resolved
            .Where(r => !existingKeys.Contains((r.CountryId, r.Dto.Date, r.Dto.Name)))
            .ToList();
        var toUpdate = resolved
            .Where(r => existingKeys.Contains((r.CountryId, r.Dto.Date, r.Dto.Name)))
            .ToList();

        int added = 0, updated = 0;

        foreach (var batch in Chunk(toInsert, BatchSize))
        {
            using var db = CreateDbContext();
            foreach (var (dto, countryId) in batch)
            {
                db.Publicholidays.Add(new Publicholiday
                {
                    Date = dto.Date,
                    Name = dto.Name,
                    LocalName = dto.LocalName,
                    Type = dto.Type,
                    CountryId = countryId
                });
                added++;
            }

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
        }

        foreach (var batch in Chunk(toUpdate, BatchSize))
        {
            using var db = CreateDbContext();

            var keys = batch
                .Select(r => new { r.CountryId, r.Dto.Date, r.Dto.Name })
                .ToList();

            var countryIds = keys.Select(k => k.CountryId).Distinct().ToList();
            var dates = keys.Select(k => k.Date).Distinct().ToList();

            var entities = await db.Publicholidays
                .Where(h => countryIds.Contains(h.CountryId) && dates.Contains(h.Date))
                .ToListAsync(ct);

            var entityLookup = entities
                .ToDictionary(h => (h.CountryId, h.Date, h.Name));

            foreach (var (dto, countryId) in batch)
            {
                if (!entityLookup.TryGetValue((countryId, dto.Date, dto.Name), out var entity))
                    continue;
                entity.LocalName = dto.LocalName;
                entity.Type = dto.Type;
                updated++;
            }

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Holidays synced: {Added} added, {Updated} updated.", added, updated);
        return (added, updated);
    }

    private CmdbContext CreateDbContext()
    {
        var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmdbContext>();
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        return db;
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }
}
