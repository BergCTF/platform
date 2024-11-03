using System.Security.Claims;
using System.Text.Json.Serialization;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Services;
using Berg.Api.Models.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using MediatR;
using Berg.Api.Notifications;

namespace Berg.Api.Controllers.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
public class ScoringController(
    ILogger<ScoringController> logger,
    CtfConfig ctfConfig,
    IMediator mediator,
    BergDbContext dbContext,
    IChallengeService challengeService,
    ScoringService scoringService) : ControllerBase
{
    private readonly object _submitFlagLock = new();
    public class SubmitFlagRequest
    {
        [JsonPropertyName("challenge")] public string? Challenge { get; set; }

        [JsonPropertyName("flag")] public string? Flag { get; set; }
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

        if (flag.Length > 1024)
            throw new ArgumentException("Submitted flag can't be longer than 1024 chars!");

        var now = DateTime.UtcNow;
        if (ctfConfig.Start > now)
            return SubmitFlagResult.CtfNotStarted;
        if (ctfConfig.End < now)
            return SubmitFlagResult.CtfHasEnded;

        lock (_submitFlagLock)
        {
            var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
            var player = dbContext.Players
                .Include(p => p.Submissions)
                .Include(p => p.Solves)
                .Include(p => p.Team)
                .FirstOrDefault(p => p.Id == playerId);
            if (player == null)
                throw new ArgumentException("Invalid player");

            if (player.Solves.Any(s => s.ChallengeId == challenge))
                return SubmitFlagResult.AlreadySolved;

            var yesterday = now.Subtract(TimeSpan.FromDays(1));
            var latestFailedSubmissions = player.Submissions
                .Where(s => yesterday < s.SubmittedAt)
                .ToList();
            if (latestFailedSubmissions.Count > ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerDay)
            {
                logger.LogWarning("Player {} has reached the daily submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }

            var oneHourAgo = now.Subtract(TimeSpan.FromHours(1));
            var submissionCountHour = latestFailedSubmissions.Count(s => oneHourAgo < s.SubmittedAt);
            if (submissionCountHour > ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerHour)
            {
                logger.LogWarning("Player {} has reached the hourly submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }

            var oneMinuteAgo = now.Subtract(TimeSpan.FromMinutes(1));
            var submissionCountMinute = latestFailedSubmissions.Count(s => oneMinuteAgo < s.SubmittedAt);
            if (submissionCountMinute > ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerMinute)
            {
                logger.LogWarning("Player {} has reached the minute submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }

            var challengeConfig = challengeService.GetChallengeConfig(challenge) ?? throw new ArgumentException("Invalid challenge");
            var dbChallenge = dbContext.Challenges.FirstOrDefault(c => c.Name == challenge) ?? throw new ArgumentException("Invalid db challenge");
            var trimmedFlag = flag.Trim();
            if (challengeConfig.Spec.Flag != trimmedFlag)
            {
                // Invalid submission
                dbContext.Submissions.Add(new Submission
                {
                    Id = UUIDNext.Uuid.NewSequential(),
                    Challenge = dbChallenge,
                    SubmittedAt = now,
                    Player = player,
                    Value = trimmedFlag
                });
                dbContext.SaveChanges();
                logger.LogInformation("Player {} submitted an invalid flag for challenge {}", playerId, challenge);
                return SubmitFlagResult.Incorrect;
            }

            var dbSolve = new Db.Solve
            {
                Id = UUIDNext.Uuid.NewSequential(),
                Challenge = dbChallenge,
                SolvedAt = now,
                Player = player,
            };
            dbContext.Solves.Add(dbSolve);
            dbContext.SaveChanges();
            logger.LogInformation("Player {} has solved challenge {}", playerId, challenge);

            var freezeStart = ctfConfig.Scoring.FreezeStart;
            var freezeEnd = ctfConfig.Scoring.FreezeEnd;
            var utcNow = DateTime.UtcNow;
            var isCurrentlyFrozen = freezeStart < utcNow && utcNow < freezeEnd;

            // Asynchronously let other components react to this solve
            mediator.Publish(new SolveNotification
            {
                Id = dbSolve.Id,
                PlayerId = player.Id,
                PlayerFederatedId = player.FederatedId,
                PlayerName = player.Name,
                TeamId = player.TeamId,
                TeamName = player.Team?.Name,
                SolvedAt = dbSolve.SolvedAt,
                Challenge = challenge,
                IsFrozen = isCurrentlyFrozen
            }).ConfigureAwait(false);

            return SubmitFlagResult.Correct;
        }
    }

    [HttpGet]
    [Route("/api/v1/scoreboard/teams")]
    public List<TeamRanking> GetTeamScoreboard()
    {
        return scoringService.GetTeamScoreboard();
    }

    [HttpGet]
    [Route("/api/v1/scoreboard/players")]
    public List<PlayerRanking> GetPlayerScoreboard(
        [FromQuery] string? attributeName = null,
        [FromQuery] string? attributeValue = null)
    {
        if (attributeName == null || attributeValue == null)
            return scoringService.GetPlayerScoreboard();

        if (!ctfConfig.PlayerAttributes?.Any(a => a.Public && a.Name == attributeName) ?? true)
            throw new ArgumentException("Can't filter by attribute that doesn't exist or is not public.");
        var firstBloods = dbContext.Challenges.Select(c => new
        {
            c.Name,
            FirstBloodedPlayerId = c.Solves
                .OrderBy(s => s.SolvedAt)
                .Where(s => s.Player.Attributes
                    .Any(a => a.Name == attributeName && a.Value == attributeValue))
                .Select(s => s.PlayerId).FirstOrDefault()
        }).ToDictionary(b => b.Name, b => b.FirstBloodedPlayerId);
        var filteredPlayers = dbContext.Players
            .Where(p => p.Attributes.Any(a => a.Name == attributeName && a.Value == attributeValue))
            .Select(p => p.Id)
            .ToHashSet();
        return scoringService.GetPlayerScoreboard()
            .Where(s => filteredPlayers.Contains(s.PlayerId))
            .Select(r =>
            {
                r.Solves = r.Solves.Select(s =>
                {
                    s.IsFirstBlood = firstBloods[s.ChallengeName] == s.PlayerId;
                    return s;
                }).ToList();
                return r;
            })
            .ToList();
    }

    [HttpGet]
    [Route("/api/v1/activity")]
    public List<ActivityEntry> GetActivity()
    {
        return dbContext.Solves
            .Include(s => s.Player)
            .OrderBy(s => s.SolvedAt)
            .Select(s => new ActivityEntry
            {
                PlayerId = s.PlayerId,
                SolvedAt = s.SolvedAt,
                ChallengeName = s.ChallengeId,
                TeamId = s.Player.TeamId
            })
            .ToList();
    }
}
