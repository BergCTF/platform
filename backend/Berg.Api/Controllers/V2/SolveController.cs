using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Notifications;
using Berg.Api.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Solve = Berg.Api.Models.V2.Solve;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class SolveController(
    ILogger<SolveController> logger,
    CtfConfig ctfConfig,
    BergDbContext dbContext,
    IChallengeService challengeService,
    IMediator mediator) : ControllerBase
{
    private readonly object _submitFlagLock = new();

    [HttpGet]
    [Route("/api/v2/solves")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [ProducesResponseType(typeof(List<Solve>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<List<Solve>> ListSolves()
    {
        var utcNow = DateTime.UtcNow;
        var freezeStart = ctfConfig.Scoring.FreezeStart;
        var freezeEnd = ctfConfig.Scoring.FreezeStart;
        var isCurrentlyFrozen = freezeStart < utcNow && utcNow < freezeEnd;
        var isUserLoggedIn = User.Identity?.IsAuthenticated ?? false;

        if (!isCurrentlyFrozen)
        {
            // If we are not in a freeze, we can show every solve
            return ToModelSolves(dbContext.Solves);
        }

        if (!isUserLoggedIn)
        {
            // We are in a freeze, but not logged in, so we can only show
            // solves that were made before the freeze
            return ToModelSolves(dbContext.Solves.Where(s => s.SolvedAt < freezeStart));
        }

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var teamId = dbContext.Players.Single(p => p.Id == playerId).TeamId;

        // We are in a freeze, and the player wants to see all own and team solves
        // if the player has joined a team
        return ToModelSolves(dbContext.Solves
            .Where(s => s.SolvedAt < freezeStart || s.PlayerId == playerId || (teamId != null && s.Player.TeamId == teamId))
        );
    }

    private static List<Solve> ToModelSolves(IQueryable<Db.Solve> solves)
    {
        return [.. solves.Select(s => new Solve
            {
                ChallengeName = s.ChallengeId,
                Id = s.Id,
                PlayerId = s.PlayerId,
                SolvedAt = s.SolvedAt
            })
        ];
    }

    public class AddSolveRequest
    {
        [JsonPropertyName("challenge")]
        public string? Challenge { get; set; }

        [JsonPropertyName("flag")]
        public string? Flag { get; set; }
    }

    [HttpPost]
    [Route("/api/v2/solves")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(Solve), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<Solve> AddSolve([FromBody] AddSolveRequest addSolveRequest)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var challengeName = addSolveRequest.Challenge;
        if (string.IsNullOrEmpty(challengeName))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Challenge can't be empty.",
            });
        }
        var challengeConfig = challengeService.GetChallengeConfig(challengeName);
        if (challengeConfig == null)
        {
            logger.LogWarning("Player {PlayerId} wanted to submit a flag for an invalid challenge: {ChallengeName}", playerId, challengeName);
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Challenge does not exist.",
            });
        }

        if (string.IsNullOrEmpty(addSolveRequest.Flag))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Flag can't be empty.",
            });
        }
        if (addSolveRequest.Flag.Length > 1024)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Submitted flag can't be longer than 1024 chars.",
            });
        }

        var utcNow = DateTime.UtcNow;
        if (ctfConfig.Start > utcNow)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid submission time",
                Detail = "CTF has not yet started."
            });
        }
        if (ctfConfig.End < utcNow)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid submission time",
                Detail = "CTF has ended."
            });
        }

        lock (_submitFlagLock)
        {
            var player = dbContext.Players
                .Include(p => p.Submissions)
                .Include(p => p.Solves)
                .Include(p => p.Team)
                .Single(p => p.Id == playerId);

            if (player.Solves.Any(s => s.ChallengeId == challengeName))
            {
                logger.LogWarning("Player {PlayerId} has already solved challenge {ChallengeName}", playerId, challengeName);
                return BadRequest(new ProblemDetails
                {
                    Title = "Already solved",
                    Detail = "You have already solved this challenge."
                });
            }

            var yesterday = utcNow.Subtract(TimeSpan.FromDays(1));
            var latestFailedSubmissions = player.Submissions
                .Where(s => yesterday < s.SubmittedAt)
                .ToList();
            if (latestFailedSubmissions.Count > ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerDay)
            {
                logger.LogWarning("Player {PlayerId} has reached the daily submission limit", playerId);
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                Response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromDays(1)).ToString();
                return new ObjectResult(new ProblemDetails
                {
                    Title = "Too many requests",
                    Detail = "You have reached the daily submission limit."
                });
            }

            var oneHourAgo = utcNow.Subtract(TimeSpan.FromHours(1));
            var submissionCountHour = latestFailedSubmissions.Count(s => oneHourAgo < s.SubmittedAt);
            if (submissionCountHour > ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerHour)
            {
                logger.LogWarning("Player {PlayerId} has reached the hourly submission limit", playerId);
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                Response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1)).ToString();
                return new ObjectResult(new ProblemDetails
                {
                    Title = "Too many requests",
                    Detail = "You have reached the hourly submission limit."
                });
            }

            var oneMinuteAgo = utcNow.Subtract(TimeSpan.FromMinutes(1));
            var submissionCountMinute = latestFailedSubmissions.Count(s => oneMinuteAgo < s.SubmittedAt);
            if (submissionCountMinute > ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerMinute)
            {
                logger.LogWarning("Player {PlayerId} has reached the minute submission limit", playerId);
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                Response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1)).ToString();
                return new ObjectResult(new ProblemDetails
                {
                    Title = "Too many requests",
                    Detail = "You have reached the minute submission limit."
                });
            }

            var dbChallenge = dbContext.Challenges.Single(c => c.Name == challengeName);

            var trimmedFlag = addSolveRequest.Flag.Trim();
            if (challengeConfig.Spec.Flag != trimmedFlag)
            {
                dbContext.Submissions.Add(new Submission
                {
                    Id = UUIDNext.Uuid.NewSequential(),
                    Challenge = dbChallenge,
                    SubmittedAt = utcNow,
                    Player = player,
                    Value = trimmedFlag
                });
                dbContext.SaveChanges();
                logger.LogInformation("Player {PlayerId} submitted an invalid flag for challenge {ChallengeName}", playerId, challengeName);
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid flag",
                    Detail = "The flag you have provided is incorrect."
                });
            }

            var dbSolve = new Db.Solve
            {
                Id = UUIDNext.Uuid.NewSequential(),
                Challenge = dbChallenge,
                SolvedAt = utcNow,
                Player = player,
            };
            dbContext.Solves.Add(dbSolve);
            dbContext.SaveChanges();
            logger.LogInformation("Player {PlayerId} has solved challenge {ChallengeName}", playerId, challengeName);

            var freezeStart = ctfConfig.Scoring.FreezeStart;
            var freezeEnd = ctfConfig.Scoring.FreezeStart;
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
                Challenge = challengeName,
                IsFrozen = isCurrentlyFrozen
            });

            return Ok(new Solve
            {
                Id = dbSolve.Id,
                PlayerId = player.Id,
                ChallengeName = challengeName,
                SolvedAt = utcNow
            });
        }
    }
}
