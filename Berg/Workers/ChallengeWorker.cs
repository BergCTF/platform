using Berg.Services;

namespace Berg.Workers;

public class ChallengeWorker : BackgroundService
{
    private readonly ILogger<ChallengeWorker> _logger;
    private readonly ChallengeService _service;

    public ChallengeWorker(ILogger<ChallengeWorker> logger, ChallengeService service)
    {
        _logger = logger;
        _service = service;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await _service.CleanupExpiredDemandedChallenges(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}