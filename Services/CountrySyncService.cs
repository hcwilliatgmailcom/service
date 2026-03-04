using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using service.Data;
using service.Models;

namespace service.Services;

public class CountrySyncService(
    IHttpClientFactory httpClientFactory,
    IServiceProvider serviceProvider,
    ILogger<CountrySyncService> logger)
    : BaseSyncService(serviceProvider)
{
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
            logger.LogError(ex, "Country sync failed.");
            return (0, 0);
        }
    }

    public async Task<List<CountryDto>> FetchAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
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

            var region     = item.TryGetProperty("region",     out var reg) ? reg.GetString() : null;
            var subregion  = item.TryGetProperty("subregion",  out var sub) ? sub.GetString() : null;
            var population = item.TryGetProperty("population", out var pop) ? (decimal?)pop.GetInt64() : null;
            var area       = item.TryGetProperty("area",       out var ar)  ? (decimal?)ar.GetDecimal() : null;

            list.Add(new CountryDto(code, name, capital, region, subregion, population, area));
        }

        logger.LogInformation("Fetched {Count} countries from API.", list.Count);
        return list;
    }

    public async Task<(int Added, int Updated)> UpsertAsync(List<CountryDto> data, CancellationToken ct)
    {
        var result = await UpsertAsync(
            data,
            loadKeys: async (db, t) => await db.Countries.AsNoTracking().Select(c => c.Code).ToHashSetAsync(t),
            keyOf:  dto => dto.Code,
            create: dto => new Country
            {
                Code       = dto.Code,
                Name       = dto.Name,
                Capital    = dto.Capital,
                Region     = dto.Region,
                Subregion  = dto.Subregion,
                Population = dto.Population,
                Area       = dto.Area
            },
            fetch: async (db, codes, t) =>
                await db.Countries
                    .Where(c => codes.Contains(c.Code))
                    .ToDictionaryAsync(c => c.Code, t),
            apply: (entity, dto) =>
            {
                entity.Name       = dto.Name;
                entity.Capital    = dto.Capital;
                entity.Region     = dto.Region;
                entity.Subregion  = dto.Subregion;
                entity.Population = dto.Population;
                entity.Area       = dto.Area;
            },
            ct);

        logger.LogInformation("Countries synced: {Added} added, {Updated} updated.", result.Added, result.Updated);
        return result;
    }
}
