using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Db;
using Berg.Shared;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class ScoringController : Controller
{
    private readonly ILogger<ScoringController> _logger;
    private readonly CtfConfig _ctfConfig;
    private readonly BergDbContext _dbContext;
    private readonly GenericClient _challengeClient;
    private readonly string _namespace;
    
    public ScoringController(
        ILogger<ScoringController> logger,
        CtfConfig ctfConfig,
        BergDbContext dbContext,
        Kubernetes kubernetes)
    {
        _logger = logger; ;
        _challengeClient = new GenericClient(kubernetes, "berg.norelect.ch", "v1", "challenges", false);
        _ctfConfig = ctfConfig;
        _dbContext = dbContext;
        _namespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }
    
    [HttpGet]
    [Route("/api/v1/flag")]
    public async Task<SubmitFlagResult> SubmitFlag(string challenge, string flag, CancellationToken cancel)
    {
        var playerId = GetPlayerId();
        var player = await _dbContext.Players.FirstOrDefaultAsync(p => p.Id == playerId, cancel);
        if (player == null)
            throw new ArgumentException("Invalid player");

        var utcNow = DateTime.UtcNow;
        var yesterday = utcNow.Subtract(TimeSpan.FromDays(1));
        var latestFailedSubmissions = player.Submissions.Where(s => yesterday < s.SubmittedAt).ToList();
        if (latestFailedSubmissions.Count > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerDay)
        {
            _logger.LogWarning("Player {} has surpassed the daily submission limit", playerId);
            return SubmitFlagResult.RateLimited;
        }

        var oneHourAgo = utcNow.Subtract(TimeSpan.FromHours(1));
        var submissionCountHour = latestFailedSubmissions.Count(s => oneHourAgo < s.SubmittedAt);
        if (submissionCountHour > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerHour)
        {
            _logger.LogWarning("Player {} has surpassed the hourly submission limit", playerId);
            return SubmitFlagResult.RateLimited;
        }
        
        var oneMinuteAgo = utcNow.Subtract(TimeSpan.FromMinutes(1));
        var submissionCountMinute = latestFailedSubmissions.Count(s => oneMinuteAgo < s.SubmittedAt);
        if (submissionCountMinute > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerMinute)
        {
            _logger.LogWarning("Player {} has surpassed the minute submission limit", playerId);
            return SubmitFlagResult.RateLimited;
        }
        
        var challengeConfig = await _challengeClient.ReadNamespacedAsync<V1Challenge>(_namespace, challenge, cancel);
        if (challengeConfig == null)
            throw new ArgumentException("Invalid challenge");
        if (challengeConfig != null && DateTime.UtcNow < challengeConfig.Spec.HideUntil)
            throw new ArgumentException("Invalid challenge");
        var challengeName = challengeConfig.Name();
        
        // TODO: Make sure that challenges in the db always match the configuration in the cluster
        var dbChallenge = await _dbContext.Challenges.FirstOrDefaultAsync(c => c.Name == challengeName, cancel);
        if (dbChallenge == null)
            throw new ArgumentException("Invalid db challenge");

        if (challengeConfig!.Spec.Flag != flag.Trim())
        {
            // Invalid submission
            _dbContext.Submissions.Add(new Submission
            {
                Id = Guid.NewGuid(),
                Challenge = dbChallenge,
                SubmittedAt = utcNow,
                Player = player,
            });
            await _dbContext.SaveChangesAsync(cancel);
            _logger.LogInformation("Player {} submitted an invalid flag for challenge {}", playerId, challengeName);
            return SubmitFlagResult.Incorrect;
        }

        // Valid submission
        _dbContext.Solves.Add(new Solve
        {
            Id = Guid.NewGuid(),
            Challenge = dbChallenge,
            SolvedAt = utcNow,
            Player = player,
        });
        await _dbContext.SaveChangesAsync(cancel);
        _logger.LogInformation("Player {} has solved challenge {}", playerId, challengeName);
        return SubmitFlagResult.Correct;
    }
    
    [HttpGet]
    [Route("/api/v1/scoreboard")]
    public async Task GetScoreboard(CancellationToken cancel)
    {
        await Task.CompletedTask;
    }
    
    private static Guid GetPlayerId()
    {
        return Guid.Empty;
    }
}