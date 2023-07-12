using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class ScoringController : Controller
{
    private readonly ILogger<ScoringController> _logger;
    private readonly CtfConfig _ctfConfig;
    private readonly BergDbContext _dbContext;
    private readonly ChallengeService _challengeService;
    private readonly ScoringService _scoringService;
    private readonly object _submitFlagLock = new();
    
    public ScoringController(
        ILogger<ScoringController> logger,
        CtfConfig ctfConfig,
        BergDbContext dbContext,
        ChallengeService challengeService,
        ScoringService scoringService)
    {
        _logger = logger;
        _challengeService = challengeService;
        _ctfConfig = ctfConfig;
        _dbContext = dbContext;
        _scoringService = scoringService;
    }
    
    [HttpGet]
    [Route("/api/v1/flag")]
    public SubmitFlagResult SubmitFlag(string challengeName, string flag)
    {
        var utcNow = DateTime.UtcNow;
        if (_ctfConfig.Start > utcNow)
            throw new ArgumentException("CTF has not started yet");
        if (_ctfConfig.End < utcNow)
            throw new ArgumentException("CTF has ended, no more flags accepted");
        
        lock (_submitFlagLock)
        {
            var playerId = GetPlayerId();
            var player = _dbContext.Players
                .Include(p => p.Team)
                .FirstOrDefault(p => p.Id == playerId);
            if (player == null)
                throw new ArgumentException("Invalid player");
            
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

            var challengeConfig = _challengeService.GetChallengeConfig(challengeName);
            if (challengeConfig == null)
                throw new ArgumentException("Invalid challenge");

            var dbChallenge = _dbContext.Challenges.FirstOrDefault(c => c.Name == challengeName);
            if (dbChallenge == null)
                throw new ArgumentException("Invalid db challenge");

            if (challengeConfig.Spec.Flag != flag.Trim())
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