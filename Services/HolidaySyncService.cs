using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using service.Models;

namespace service.Services;

public class HolidaySyncService(
    IHttpClientFactory httpClientFactory,
    IServiceProvider serviceProvider,
    ILogger<HolidaySyncService> logger)
    : BaseSyncService(serviceProvider)
{
    private const int MaxParallelFetches = 10;

    public record HolidayDto(string CountryCode, DateTime Date, string Name, string? LocalName, string? Type);

    // Intermediate record with resolved CountryId — used only inside UpsertAsync
    private record ResolvedHolidayDto(decimal CountryId, DateTime Date, string Name, string? LocalName, string? Type);

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
            logger.LogError(ex, "Holiday sync failed.");
            return (0, 0);
        }
    }

    public async Task<List<HolidayDto>> FetchAsync(List<string> countryCodes, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
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

                    var name      = item.TryGetProperty("name",      out var n)  ? n.GetString()  ?? "" : "";
                    var localName = item.TryGetProperty("localName", out var ln) ? ln.GetString() : null;
                    var type      = item.TryGetProperty("type",      out var t)  ? t.GetString()  : null;

                    batch.Add(new HolidayDto(code, date, name, localName, type));
                }

                lock (lockObj) { results.AddRange(batch); }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch holidays for {Code}.", code);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        logger.LogInformation("Fetched {Count} holidays from API.", results.Count);
        return results;
    }

    public async Task<(int Added, int Updated)> UpsertAsync(List<HolidayDto> data, CancellationToken ct)
    {
        // Resolve CountryCode → CountryId before handing off to the generic loop
        Dictionary<string, decimal> countryByCode;
        using (var db = CreateDbContext())
        {
            countryByCode = await db.Countries
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Code, c => c.Id, ct);
        }

        var resolved = data
            .Where(d => countryByCode.ContainsKey(d.CountryCode))
            .Select(d => new ResolvedHolidayDto(countryByCode[d.CountryCode], d.Date, d.Name, d.LocalName, d.Type))
            .ToList();

        var result = await UpsertAsync(
            resolved,
            loadKeys: async (db, t) =>
                (await db.Publicholidays
                    .AsNoTracking()
                    .Select(h => new { h.CountryId, h.Date, h.Name })
                    .ToListAsync(t))
                .Select(h => (h.CountryId, h.Date, h.Name))
                .ToHashSet(),
            keyOf: dto => (dto.CountryId, dto.Date, dto.Name),
            create: dto => new Publicholiday
            {
                CountryId = dto.CountryId,
                Date      = dto.Date,
                Name      = dto.Name,
                LocalName = dto.LocalName,
                Type      = dto.Type
            },
            fetch: async (db, keys, t) =>
            {
                var countryIds = keys.Select(k => k.CountryId).Distinct().ToList();
                var dates      = keys.Select(k => k.Date).Distinct().ToList();
                var entities   = await db.Publicholidays
                    .Where(h => countryIds.Contains(h.CountryId) && dates.Contains(h.Date))
                    .ToListAsync(t);
                return entities.ToDictionary(h => (h.CountryId, h.Date, h.Name));
            },
            apply: (entity, dto) =>
            {
                entity.LocalName = dto.LocalName;
                entity.Type      = dto.Type;
            },
            ct);

        logger.LogInformation("Holidays synced: {Added} added, {Updated} updated.", result.Added, result.Updated);
        return result;
    }
}
