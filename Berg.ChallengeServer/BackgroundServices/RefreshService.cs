using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;

namespace Berg.ChallengeServer.BackgroundServices;

public class RefreshService : BackgroundService
{
    private readonly ILogger<RefreshService> _logger;
    private readonly ScoringService _scoringService;
    private readonly IChallengeService _challengeService;
    private readonly PlayerService _playerService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly InfraConfig _infraConfig;

    public RefreshService(
        ILogger<RefreshService> logger,
        ScoringService scoringService,
        IChallengeService challengeService,
        PlayerService playerService,
        IServiceScopeFactory serviceScopeFactory,
        InfraConfig infraConfig)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _scoringService = scoringService;
        _challengeService = challengeService;
        _playerService = playerService;
        _infraConfig = infraConfig;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RefreshService started");
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using (var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>())
            {
                _challengeService.RefreshChallenges(dbContext);
                _scoringService.RefreshScores(dbContext);
                _playerService.RefreshPlayerInfo(dbContext);
            }
            await _challengeService.CheckChallengeInstanceTimeout(stoppingToken);

            await Task.Delay(_infraConfig.RefreshInterval, stoppingToken);
        }
        _logger.LogInformation("RefreshService stopped");
    }
}