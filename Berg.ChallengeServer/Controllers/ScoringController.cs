using System.Text.Json.Serialization;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class ScoringController : ControllerBase
{
    private readonly ILogger<ScoringController> _logger;
    private readonly CtfConfig _ctfConfig;
    private readonly BergDbContext _dbContext;
    private readonly ChallengeService _challengeService;
    private readonly ScoringService _scoringService;
    private readonly PlayerService _playerService;
    private readonly object _submitFlagLock = new();
    
    public ScoringController(
        ILogger<ScoringController> logger,
        CtfConfig ctfConfig,
        BergDbContext dbContext,
        ChallengeService challengeService,
        ScoringService scoringService,
        PlayerService playerService)
    {
        _logger = logger;
        _challengeService = challengeService;
        _ctfConfig = ctfConfig;
        _dbContext = dbContext;
        _scoringService = scoringService;
        _playerService = playerService;
    }

    public class SubmitFlagRequest
    {
        [JsonPropertyName("challenge")]
        public string? Challenge { get; set; }
        
        [JsonPropertyName("flag")]
        public string? Flag { get; set; }
    }
    
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/flag")]
    public SubmitFlagResult SubmitFlag([FromBody] SubmitFlagRequest flagRequest)
    {
        var challenge = flagRequest.Challenge;
        var flag = flagRequest.Flag;
        if (challenge == null || flag == null)
            throw new ArgumentException("Values can't be null");
        
        var utcNow = DateTime.UtcNow;
        if (_ctfConfig.Start > utcNow)
            throw new ArgumentException("CTF has not started yet");
        if (_ctfConfig.End < utcNow)
            throw new ArgumentException("CTF has ended, no more flags accepted");
        
        lock (_submitFlagLock)
        {
            var playerId = _playerService.GetPlayer(User).Id;
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

            var challengeConfig = _challengeService.GetChallengeConfig(challenge);
            if (challengeConfig == null)
                throw new ArgumentException("Invalid challenge");

            var dbChallenge = _dbContext.Challenges.FirstOrDefault(c => c.Name == challenge);
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
                _logger.LogInformation("Player {} submitted an invalid flag for challenge {}", playerId, challenge);
                return SubmitFlagResult.Incorrect;
            }

            // Valid submission
            // TODO: Send discord notification, send special message if it is a first blood.
            _dbContext.Solves.Add(new Solve
            {
                Id = Guid.NewGuid(),
                Challenge = dbChallenge,
                SolvedAt = utcNow,
                Player = player,
            });
            _dbContext.SaveChanges();
            _logger.LogInformation("Player {} has solved challenge {}", playerId, challenge);
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
}