using Berg.Api.Services;

namespace Berg.Api.BackgroundServices;

public class RefreshService(
    ILogger<RefreshService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IWebSocketService webSocketService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RefreshService started");
        var delay = TimeSpan.FromSeconds(1);
        var window = TimeSpan.FromSeconds(2);
        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = Constants.BergActivitySource.StartActivity("Refresh");
            using var scope = serviceScopeFactory.CreateScope();
            var challengeService = scope.ServiceProvider.GetRequiredService<IChallengeService>();
            await challengeService.CheckChallengeInstanceTimeout(stoppingToken);
            await challengeService.CheckNewlyUnhiddenChallenges(window, stoppingToken);
            await webSocketService.DowngradeExpiredConnections(stoppingToken);
            activity?.Stop();

            await Task.Delay(delay, stoppingToken);
        }
        logger.LogInformation("RefreshService stopped");
    }
}