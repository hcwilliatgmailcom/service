using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using service.Data;
using service.Models;

namespace service.Services;

public class CountrySyncService
{
    private const int BatchSize = 500;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CountrySyncService> _logger;

    public CountrySyncService(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<CountrySyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public record CountryDto(string Code, string Name, string? Capital, string? Region, string? Subregion, decimal? Population, decimal? Area);

    public async Task<(int Added, int Updated)> SyncAsync(CancellationToken ct)
    {
        try
        {
            var data = await FetchAsync(ct);
            return await UpsertAsync(data, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Country sync failed.");
            return (0, 0);
        }
    }

    public async Task<List<CountryDto>> FetchAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var json = await client.GetStringAsync(
            "https://restcountries.com/v3.1/all?fields=name,cca2,capital,region,subregion,population,area", ct);

        using var doc = JsonDocument.Parse(json);
        var list = new List<CountryDto>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var code = item.TryGetProperty("cca2", out var cca2) ? cca2.GetString() : null;
            if (string.IsNullOrEmpty(code)) continue;

            var name = item.TryGetProperty("name", out var nameObj)
                && nameObj.TryGetProperty("common", out var common)
                    ? common.GetString() ?? code
                    : code;

            var capital = item.TryGetProperty("capital", out var capArr)
                && capArr.GetArrayLength() > 0
                    ? capArr[0].GetString()
                    : null;

            var region = item.TryGetProperty("region", out var reg) ? reg.GetString() : null;
            var subregion = item.TryGetProperty("subregion", out var sub) ? sub.GetString() : null;
            var population = item.TryGetProperty("population", out var pop) ? (decimal?)pop.GetInt64() : null;
            var area = item.TryGetProperty("area", out var ar) ? (decimal?)ar.GetDecimal() : null;

            list.Add(new CountryDto(code, name, capital, region, subregion, population, area));
        }

        _logger.LogInformation("Fetched {Count} countries from API.", list.Count);
        return list;
    }

    public async Task<(int Added, int Updated)> UpsertAsync(List<CountryDto> data, CancellationToken ct)
    {
        Dictionary<string, decimal> codeToId;
        using (var readDb = CreateDbContext())
        {
            codeToId = await readDb.Countries
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Code, c => c.Id, ct);
        }

        int added = 0, updated = 0;

        var toUpdate = data.Where(d => codeToId.ContainsKey(d.Code)).ToList();
        foreach (var batch in Chunk(toUpdate, BatchSize))
        {
            using var db = CreateDbContext();
            var codes = batch.Select(d => d.Code).ToHashSet();
            var entities = await db.Countries
                .Where(c => codes.Contains(c.Code))
                .ToDictionaryAsync(c => c.Code, ct);

            foreach (var dto in batch)
            {
                if (!entities.TryGetValue(dto.Code, out var entity)) continue;
                entity.Name = dto.Name;
                entity.Capital = dto.Capital;
                entity.Region = dto.Region;
                entity.Subregion = dto.Subregion;
                entity.Population = dto.Population;
                entity.Area = dto.Area;
                updated++;
            }

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
        }

        var toInsert = data.Where(d => !codeToId.ContainsKey(d.Code)).ToList();
        foreach (var batch in Chunk(toInsert, BatchSize))
        {
            using var db = CreateDbContext();
            foreach (var dto in batch)
            {
                db.Countries.Add(new Country
                {
                    Code = dto.Code,
                    Name = dto.Name,
                    Capital = dto.Capital,
                    Region = dto.Region,
                    Subregion = dto.Subregion,
                    Population = dto.Population,
                    Area = dto.Area
                });
                added++;
            }

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Countries synced: {Added} added, {Updated} updated.", added, updated);
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
