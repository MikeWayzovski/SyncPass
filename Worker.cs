using TrimbleConnector.Services;

namespace TrimbleConnector;

public sealed class Worker : BackgroundService
{
    private readonly SyncEngine _engine;
    private readonly ILogger<Worker> _logger;

    public Worker(SyncEngine engine, ILogger<Worker> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Product} web setup and sync daemon started.", ProductInfo.DisplayName);
        try
        {
            await _engine.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("{Product} daemon is stopping.", ProductInfo.DisplayName);
        }
    }
}
