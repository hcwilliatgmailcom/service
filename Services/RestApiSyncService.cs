using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace service.Services;

public class RestApiSyncService : BackgroundService
{
    private readonly CountrySyncService _countrySyncService;
    private readonly HolidaySyncService _holidaySyncService;
    private readonly ILogger<RestApiSyncService> _logger;

    public RestApiSyncService(
        CountrySyncService countrySyncService,
        HolidaySyncService holidaySyncService,
        ILogger<RestApiSyncService> logger)
    {
        _countrySyncService = countrySyncService;
        _holidaySyncService = holidaySyncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSyncAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        _logger.LogInformation("REST API sync starting...");
        //await _countrySyncService.SyncAsync(ct);
        //await _holidaySyncService.SyncAsync(ct);
        _logger.LogInformation("REST API sync completed.");
    }
}
