using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Services;

namespace Berg.Api.BackgroundServices;

public class RefreshService(
    ILogger<RefreshService> logger,
    IChallengeService challengeService,
    IServiceScopeFactory serviceScopeFactory,
    IWebSocketService webSocketService,
    InfraConfig infraConfig) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RefreshService started");
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = serviceScopeFactory.CreateScope();
            using var activity = Constants.BergActivitySource.StartActivity("Refresh");
            await using (var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>())
            {
                challengeService.RefreshChallenges(dbContext);
            }
            await challengeService.CheckChallengeInstanceTimeout(stoppingToken);
            await webSocketService.DowngradeExpiredConnections(stoppingToken);
            activity?.Stop();

            await Task.Delay(infraConfig.RefreshInterval, stoppingToken);
        }
        logger.LogInformation("RefreshService stopped");
    }
}