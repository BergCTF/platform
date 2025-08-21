using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Team = Berg.Api.Models.Team;
using CurrentTeam = Berg.Api.Models.CurrentTeam;
using MediatR;
using Berg.Api.Notifications;
using Berg.Api.Services;

namespace Berg.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName="berg-api")]
public partial class TeamController(
    ILogger<TeamController> logger,
    BergDbContext dbContext,
    BergMetrics metrics,
    CtfConfig ctfConfig,
    IMediator mediator) : ControllerBase
{

    [HttpGet]
    [Route("/api/teams")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [ProducesResponseType(typeof(List<Team>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<Team>>> ListTeams(CancellationToken cancel)
    {
        return Ok(await dbContext.Teams.Select(t => new Team
        {
            Id = t.Id,
            Name = t.Name,
            Players = t.Players.Select(p => p.Id).ToList()
        }).ToListAsync(cancel));
    }

    [HttpGet]
    [Route("/api/teams/{id:guid}")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [ProducesResponseType(typeof(List<Team>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<Team>>> GetTeam([FromRoute] Guid id, CancellationToken cancel)
    {
        var team = await dbContext.Teams.Select(t => new Team
        {
            Id = t.Id,
            Name = t.Name,
            Players = t.Players.Select(p => p.Id).ToList()
        }).FirstOrDefaultAsync(t => t.Id == id, cancel);
        if (team == null)
            return NotFound();
        return Ok(team);
    }

    [HttpGet]
    [Route("/api/teams/current")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(CurrentTeam), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CurrentTeam>> GetCurrentTeam(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await dbContext.Players
            .Include(p => p.Team)
            .SingleAsync(p => p.Id == playerId, cancel);
        if(!ctfConfig.Teams)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Teams are disabled"
            });
        }
        if (player.Team == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "You are not in a team"
            });
        }

        var dbTeam = dbContext.Teams
            .Include(t => t.Players)
            .Single(t => t.Id == player.TeamId);
        return Ok(new CurrentTeam
        {
            Id = player.Team.Id,
            Name = player.Team.Name,
            JoinToken = player.Team.JoinToken,
            Players = dbTeam.Players.Select(p => p.Id).ToList(),
        });
    }

    public class TeamCreateRequest
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    [GeneratedRegex(@"^[\w\d\p{P}\p{S} ]{1,32}$")]
    private static partial Regex TeamNameRegex();

    [HttpPost]
    [Route("/api/teams/create")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(CurrentTeam), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CurrentTeam>> CreateTeam([FromBody] TeamCreateRequest teamCreateRequest, CancellationToken cancel)
    {
        var teamName = teamCreateRequest.Name;
        if(!ctfConfig.Teams)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Teams are disabled"
            });
        }
        if(string.IsNullOrEmpty(teamName))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "A name must be set"
            });
        }
        if(!TeamNameRegex().IsMatch(teamName))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = $"Team name doesn't match required pattern: {TeamNameRegex()}"
            });
        }

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await dbContext.Players
            .Include(p => p.Team)
            .SingleAsync(p => p.Id == playerId, cancel);

        if (player.Team != null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "You are already in a team"
            });
        }

        if(dbContext.Teams.Any(t => t.Name == teamName))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "This team name is already taken"
            });
        }

        // Create team
        var dbTeam = new Db.Team
        {
            Id = UUIDNext.Uuid.NewSequential(),
            Name = teamName,
            JoinToken = CreateJoinToken()
        };
        dbContext.Teams.Add(dbTeam);

        // Add player to team
        player.Team = dbTeam;
        await dbContext.SaveChangesAsync(cancel);

        var _ = mediator.Publish(new TeamCreateNotification
        {
            Team = new Team
            {
                Id = dbTeam.Id,
                Name = dbTeam.Name,
                Players = [player.Id]
            }
        }, cancel);
        logger.LogInformation("Player {PlayerId} created team: {TeamId}", playerId, dbTeam.Id);
        metrics.TeamCreated(playerId);

        return Ok(new CurrentTeam
        {
            Id = dbTeam.Id,
            Name = dbTeam.Name,
            JoinToken = dbTeam.JoinToken,
            Players = [playerId]
        });
    }

    public class JoinTeamRequest
    {
        [JsonPropertyName("joinToken")]
        public string? JoinToken { get; set; }
    }

    [HttpPost]
    [Route("/api/teams/join")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(CurrentTeam), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CurrentTeam>> JoinTeam([FromBody] JoinTeamRequest req, CancellationToken cancel)
    {
        if(!ctfConfig.Teams)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Teams are disabled"
            });
        }
        if(string.IsNullOrEmpty(req.JoinToken))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "A join token must be provided"
            });
        }
        var joinToken = req.JoinToken.Trim();

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await dbContext.Players
            .Include(p => p.Team)
            .SingleAsync(p => p.Id == playerId, cancel);

        if (player.Team != null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "You are already in a team"
            });
        }

        var dbTeam = await dbContext.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.JoinToken == joinToken, cancel);
        if (dbTeam == null)
        {
            logger.LogWarning("Player {PlayerId} tried to use an invalid team join token", playerId);
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Invalid join token"
            });
        }

        // Assign the player to the team
        player.Team = dbTeam;
        await dbContext.SaveChangesAsync(cancel);
        logger.LogInformation("Player {PlayerId} joined team: {TeamId}", playerId, dbTeam.Id);

        // Add our new player to the list of players as we fetched the db information
        // before assigning the user to the team.
        var playerIds = dbTeam.Players.Select(p => p.Id).ToList();
        playerIds.Add(player.Id);
        var playerIdsDeduplicated = playerIds.Distinct().ToList();

        var _ = mediator.Publish(new TeamUpdateNotification
        {
            Team = new Team
            {
                Id = dbTeam.Id,
                Name = dbTeam.Name,
                Players = playerIdsDeduplicated,
            }
        }, cancel);

        return Ok(new CurrentTeam
        {
            Id = dbTeam.Id,
            Name = dbTeam.Name,
            JoinToken = dbTeam.JoinToken,
            Players = playerIdsDeduplicated,
        });
    }

    [HttpDelete]
    [Route("/api/teams/current")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(CurrentTeam), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> LeaveCurrentTeam()
    {
        if(!ctfConfig.Teams)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Teams are disabled"
            });
        }

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await dbContext.Players
            .Include(p => p.Team)
            .SingleAsync(p => p.Id == playerId);

        if (player.Team == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "You are not in a team"
            });
        }

        var previousTeam = player.Team;
        player.Team = null;
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Player {PlayerId} left team {TeamId}", playerId, previousTeam.Id);

        var newTeamPlayerIds = dbContext.Players
            .Where(p => p.TeamId == previousTeam.Id)
            .Select(p => p.Id)
            .ToList();

        if (newTeamPlayerIds.Count == 0)
        {
            dbContext.Teams.Remove(previousTeam);
            metrics.TeamDeleted();
            logger.LogInformation("Deleting Team {TeamId} since there are no more players in it.", previousTeam.Id);
        }
        await dbContext.SaveChangesAsync();

        if (newTeamPlayerIds.Count == 0) {
            var _ = mediator.Publish(new TeamDeleteNotification
            {
                TeamId = previousTeam.Id,
            });
        } else {
            var _ = mediator.Publish(new TeamUpdateNotification
            {
                Team = new Team
                {
                    Id = previousTeam.Id,
                    Name = previousTeam.Name,
                    Players = newTeamPlayerIds,
                }
            });
        }

        return Ok();
    }

    private static readonly RandomNumberGenerator Random = RandomNumberGenerator.Create();

    private static string CreateJoinToken()
    {
        var buf = new byte[32];
        Random.GetBytes(buf);
        return Convert.ToHexString(buf);
    }
}