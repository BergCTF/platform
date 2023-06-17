using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Db;
using k8s;
using k8s.Models;
using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.BackgroundServices;

public class ChallengeSyncService : BackgroundService
{
    private readonly ILogger<ChallengeSyncService> _logger;
    private readonly BergDbContext _dbContext;
    private readonly GenericClient _challengeClient;
    private readonly CtfConfig _ctfConfig;
    private readonly string _namespace;

    public ChallengeSyncService(
        ILogger<ChallengeSyncService> logger,
        BergDbContext dbContext,
        Kubernetes kubernetes,
        CtfConfig ctfConfig)
    {
        _logger = logger;
        _dbContext = dbContext;
        _ctfConfig = ctfConfig;
        _challengeClient = new GenericClient(kubernetes, "berg.norelect.ch", "v1", "challenges", false);
        _namespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChallengeSyncService started");
        while (!stoppingToken.IsCancellationRequested)
        {
            var configChallenges = await _challengeClient
                .ListNamespacedAsync<V1BergCustomResourceList<V1Challenge>>(_namespace, stoppingToken);
            var dbChallenges = await _dbContext.Challenges.ToListAsync(stoppingToken);
            
            // Challenges that exist only in the db need to be kept, to not influence scoring of past challenges.
            var missingChallengeNames = configChallenges.Items.Select(c => c.Name()).ToHashSet();
            missingChallengeNames.ExceptWith(dbChallenges.Select(c => c.Name));

            foreach (var missingChallengeName in missingChallengeNames)
            {
                _dbContext.Challenges.Add(new Challenge { Name = missingChallengeName });
                _logger.LogInformation("Synchronized challenge {}", missingChallengeName);
            }
            await _dbContext.SaveChangesAsync(stoppingToken);
            
            await Task.Delay(_ctfConfig.ConfigDbSyncInterval, stoppingToken);
        }
        _logger.LogInformation("ChallengeSyncService stopped");
    }
}