using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;

namespace Berg.ChallengeServer.BackgroundServices;

public class RefreshService : BackgroundService
{
    private readonly ILogger<RefreshService> _logger;
    private readonly ScoringService _scoringService;
    private readonly ChallengeService _challengeService;
    private readonly PlayerService _playerService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly CtfConfig _ctfConfig;

    public RefreshService(
        ILogger<RefreshService> logger,
        ScoringService scoringService,
        ChallengeService challengeService,
        PlayerService playerService,
        IServiceScopeFactory serviceScopeFactory,
        CtfConfig ctfConfig)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _scoringService = scoringService;
        _challengeService = challengeService;
        _playerService = playerService;
        _ctfConfig = ctfConfig;
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
            await _challengeService.CheckChallengeInstanceTimout(stoppingToken);
            
            await Task.Delay(_ctfConfig.RefreshInterval, stoppingToken);
        }
        _logger.LogInformation("RefreshService stopped");
    }
}