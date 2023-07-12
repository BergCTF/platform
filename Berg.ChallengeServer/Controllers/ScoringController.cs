using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
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
    private readonly ScoringService _scoringService;
    private readonly object _submitFlagLock = new();
    
    public ScoringController(
        ILogger<ScoringController> logger,
        CtfConfig ctfConfig,
        BergDbContext dbContext,
        Kubernetes kubernetes,
        ScoringService scoringService)
    {
        _logger = logger;
        _challengeClient = new GenericClient(kubernetes, "berg.norelect.ch", "v1", "challenges", false);
        _ctfConfig = ctfConfig;
        _dbContext = dbContext;
        _scoringService = scoringService;
        _namespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }
    
    [HttpGet]
    [Route("/api/v1/flag")]
    public SubmitFlagResult SubmitFlag(string challenge, string flag)
    {
        lock (_submitFlagLock)
        {
            var playerId = GetPlayerId();
            var player = _dbContext.Players
                .Include(p => p.Team)
                .FirstOrDefault(p => p.Id == playerId);
            if (player == null)
                throw new ArgumentException("Invalid player");
            
            var utcNow = DateTime.UtcNow;
            var yesterday = utcNow.Subtract(TimeSpan.FromDays(1));
            var latestFailedSubmissions = player.Submissions.Where(s => yesterday < s.SubmittedAt).ToList();
            if (latestFailedSubmissions.Count > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerDay)
            {
                _logger.LogWarning("Player {} has reached the daily submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }

            var oneHourAgo = utcNow.Subtract(TimeSpan.FromHours(1));
            var submissionCountHour = latestFailedSubmissions.Count(s => oneHourAgo < s.SubmittedAt);
            if (submissionCountHour > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerHour)
            {
                _logger.LogWarning("Player {} has reached the hourly submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }
            
            var oneMinuteAgo = utcNow.Subtract(TimeSpan.FromMinutes(1));
            var submissionCountMinute = latestFailedSubmissions.Count(s => oneMinuteAgo < s.SubmittedAt);
            if (submissionCountMinute > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerMinute)
            {
                _logger.LogWarning("Player {} has reached the minute submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }
            
            var challengeConfig = _challengeClient.ReadNamespacedAsync<V1Challenge>(_namespace, challenge).Result;
            if (challengeConfig == null)
                throw new ArgumentException("Invalid challenge");
            if (challengeConfig != null && DateTime.UtcNow < challengeConfig.Spec.HideUntil)
                throw new ArgumentException("Invalid challenge");
            var challengeName = challengeConfig.Name();
            
            var dbChallenge = _dbContext.Challenges.FirstOrDefault(c => c.Name == challengeName);
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
                _dbContext.SaveChanges();
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
            _dbContext.SaveChanges();
            _logger.LogInformation("Player {} has solved challenge {}", playerId, challengeName);
            return SubmitFlagResult.Correct;
        }
    }
    
    [HttpGet]
    [Route("/api/v1/scoreboard/teams")]
    public List<TeamRanking> GetTeamScoreboard()
    {
        return _scoringService.GetTeamScoreboard();
    }
    
    [HttpGet]
    [Route("/api/v1/scoreboard/players")]
    public List<PlayerRanking> GetPlayerScoreboard()
    {
        return _scoringService.GetPlayerScoreboard();
    }

    private static Guid GetPlayerId()
    {
        return Guid.Empty;
    }
}