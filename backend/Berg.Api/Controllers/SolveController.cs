using System.Security.Claims;
using System.Text.Json.Serialization;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Notifications;
using Berg.Api.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Solve = Berg.Api.Models.Solve;
using Submission = Berg.Api.Models.Submission;

namespace Berg.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "berg-api")]
public class SolveController(
    ILogger<SolveController> logger,
    CtfConfig ctfConfig,
    BergDbContext dbContext,
    IChallengeService challengeService,
    BergMetrics metrics,
    IMediator mediator) : ControllerBase
{
    private static IQueryable<Db.Solve> FilterAdminSolves(IQueryable<Db.Solve> solves, bool isAdmin)
    {
        if (isAdmin)
        {
            return solves;
        }
        return solves.Where(s => s.Player.Roles == null || !s.Player.Roles.Contains(Constants.Roles.Admin));
    }

    [HttpGet]
    [Route("/api/solves")]
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
        var isAdmin = User.HasClaim(OpenIddictConstants.Claims.Role, Constants.Roles.Admin);

        if (!isCurrentlyFrozen)
        {
            // If we are not in a freeze, we can show every solve
            return ToModelSolves(FilterAdminSolves(dbContext.Solves, isAdmin));
        }

        if (!isUserLoggedIn)
        {
            // We are in a freeze, but not logged in, so we can only show
            // solves that were made before the freeze
            return ToModelSolves(FilterAdminSolves(dbContext.Solves.Where(s => s.SolvedAt < freezeStart), isAdmin));
        }
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var teamId = dbContext.Players.Single(p => p.Id == playerId).TeamId;

        // We are in a freeze, and the player wants to see all own and team solves
        return ToModelSolves(FilterAdminSolves(dbContext.Solves
            .Where(s => s.SolvedAt < freezeStart || s.PlayerId == playerId || (teamId != null && s.Player.TeamId == teamId) || isAdmin), isAdmin)
        );
    }

    private static List<Solve> ToModelSolves(IQueryable<Db.Solve> solves)
    {
        return [.. solves.Select(s => new Solve
            {
                ChallengeName = s.ChallengeId,
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
    [Route("/api/solves")]
    [Authorize(Policy = Constants.Policies.Player)]
    [EnableRateLimiting(Constants.RateLimiting.TokenBucket)]
    [ProducesResponseType(typeof(Solve), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public ActionResult<Solve> AddSolve([FromBody] AddSolveRequest addSolveRequest)
    {
        var utcNow = DateTime.UtcNow;
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var isAdmin = User.HasClaim(OpenIddictConstants.Claims.Role, Constants.Roles.Admin);
        var challengeName = addSolveRequest.Challenge;
        if (string.IsNullOrEmpty(challengeName))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Challenge can't be empty.",
            });
        }
        var challengeConfig = challengeService.GetChallenge(challengeName, CancellationToken.None).Result;
        if (challengeConfig == null)
        {
            logger.LogWarning("Player {PlayerId} wanted to submit a flag for an invalid challenge: {ChallengeName}", playerId, challengeName);
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Challenge does not exist.",
            });
        }
        if (challengeConfig.Spec.HideUntil != null && utcNow < challengeConfig.Spec.HideUntil && !isAdmin)
        {
            logger.LogWarning("Player {PlayerId} wanted to submit a flag for a hidden challenge: {ChallengeName}", playerId, challengeName);
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
        if (utcNow < ctfConfig.Start && !isAdmin)
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

        var dbChallenge = dbContext.Challenges.Single(c => c.Name == challengeName);

        var trimmedFlag = addSolveRequest.Flag.Trim();

        var flagValid = false;
        if (challengeConfig.Spec.SupportsDynamicFlags)
        {
            if (ctfConfig.Teams)
            {
                // Allow team members to submit a dynamic flag of their teammates
                flagValid = dbContext.Instances.Any(i => i.Player.TeamId == player.TeamId &&
                    i.ChallengeName == challengeName &&
                    i.DynamicFlag == trimmedFlag);
            }
            else
            {
                flagValid = dbContext.Instances.Any(i => i.PlayerId == playerId &&
                    i.ChallengeName == challengeName &&
                    i.DynamicFlag == trimmedFlag);
            }
        }
        else
        {
            flagValid = challengeConfig.Spec.Flag == trimmedFlag;
        }

        if (!flagValid)
        {
            dbContext.Submissions.Add(new Db.Submission
            {
                Id = UUIDNext.Uuid.NewSequential(),
                Challenge = dbChallenge,
                SubmittedAt = utcNow,
                Player = player,
                Value = trimmedFlag
            });
            dbContext.SaveChanges();
            logger.LogInformation("Player {PlayerId} submitted an invalid flag for challenge {ChallengeName}", playerId, challengeName);
            metrics.InvalidSubmission(challengeName, playerId);
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid flag",
                Detail = "The flag you have provided is incorrect."
            });
        }

        var dbSolve = new Db.Solve
        {
            Challenge = dbChallenge,
            SolvedAt = utcNow,
            Player = player,
        };
        dbContext.Solves.Add(dbSolve);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            // Log update exception, most probable cause for this is a unique constraint violation if a player tries to submit
            // a solve for the same challenge in a very short timeframe.
            logger.LogError(ex, "Failed to save player {PlayerId} solve for challenge {ChallengeName}", playerId, challengeName);
            return Conflict(new ProblemDetails
            {
                Title = "Saving solve failed",
                Detail = "Failed to save player solve for this challenge."
            });
        }
        logger.LogInformation("Player {PlayerId} has solved challenge {ChallengeName}", playerId, challengeName);
        metrics.ValidSubmission(challengeName, playerId);

        var freezeStart = ctfConfig.Scoring.FreezeStart;
        var freezeEnd = ctfConfig.Scoring.FreezeStart;
        var isCurrentlyFrozen = freezeStart < utcNow && utcNow < freezeEnd;

        // Asynchronously let other components react to this solve
        var _ = mediator.Publish(new SolveNotification
        {
            PlayerId = player.Id,
            PlayerFederatedId = player.FederatedId,
            PlayerName = player.Name,
            TeamId = player.TeamId,
            TeamName = player.Team?.Name,
            SolvedAt = dbSolve.SolvedAt,
            Challenge = challengeName,
            IsFrozen = isCurrentlyFrozen,
            IsAdmin = isAdmin,
        });

        return Ok(new Solve
        {
            PlayerId = player.Id,
            ChallengeName = challengeName,
            SolvedAt = utcNow
        });
    }

    [HttpGet]
    [Route("/api/submissions")]
    [Authorize(Policy = Constants.Policies.Admin)]
    [ProducesResponseType(typeof(List<Submission>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<List<Submission>> ListSubmissions()
    {
        return dbContext.Submissions.Select(s => new Submission
        {
            Id = s.Id,
            PlayerId = s.Player.Id,
            Value = s.Value,
            SubmittedAt = s.SubmittedAt,
            ChallengeName = s.Challenge.Name,
        }).ToList();
    }
}
