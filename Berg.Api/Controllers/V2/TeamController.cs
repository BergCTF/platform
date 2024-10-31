using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Team = Berg.Api.Models.V2.Team;
using OwnTeam = Berg.Api.Models.V2.OwnTeam;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public partial class TeamController(
    ILogger<TeamController> logger,
    BergDbContext dbContext,
    ScoringService scoringService,
    CtfConfig ctfConfig) : ControllerBase
{

    [HttpGet]
    [Route("/api/v2/teams")]
    public async Task<List<Team>> ListTeams(CancellationToken cancel)
    {
        return await dbContext.Teams.Select(t => new Team
        {
            Id = t.Id,
            Name = t.Name,
            Players = t.Players.Select(p => p.Id).ToList()
        }).ToListAsync(cancel);
    }

    [HttpGet]
    [Authorize]
    [Route("/api/v2/teams/own")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OwnTeam>> GetOwnTeam(CancellationToken cancel)
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
        return Ok(new OwnTeam
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

    [GeneratedRegex("^[\\w\\d]{1,32}$")]
    private static partial Regex TeamNameRegex();

    [HttpPost]
    [Authorize]
    [Route("/api/v2/teams/own")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OwnTeam>> CreateTeam([FromBody] TeamCreateRequest teamCreateRequest, CancellationToken cancel)
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
        await dbContext.Teams.AddAsync(dbTeam, cancel);

        // Add player to team
        player.Team = dbTeam;
        await dbContext.SaveChangesAsync(cancel);
        scoringService.RefreshScores(dbContext);
        logger.LogInformation("Player {PlayerId} created team: {TeamId}", playerId, dbTeam.Id);

        return Ok(new OwnTeam
        {
            Id = dbTeam.Id,
            JoinToken = dbTeam.JoinToken,
            Players = [playerId]
        });
    }

    public class JoinTeamRequest
    {
        [JsonPropertyName("joinToken")]
        public string? JoinToken { get; set; }
    }

    [HttpPatch]
    [Authorize]
    [Route("/api/v2/teams/own")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OwnTeam>> JoinTeam([FromBody] JoinTeamRequest req, CancellationToken cancel)
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
        scoringService.RefreshScores(dbContext);
        logger.LogInformation("Player {PlayerId} joined team: {TeamId}", playerId, dbTeam.Id);

        // Add our new player to the list of players as we fetched the db information
        // before assigning the user to the team.
        var playerIds = dbTeam.Players.Select(p => p.Id).ToList();
        playerIds.Add(player.Id);

        return Ok(new OwnTeam
        {
            Id = dbTeam.Id,
            Name = dbTeam.Name,
            JoinToken = dbTeam.JoinToken,
            Players = playerIds.Distinct().ToList(),
        });
    }

    [HttpDelete]
    [Authorize]
    [Route("/api/v2/teams/own")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> LeaveTeam(CancellationToken cancel)
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
            .SingleAsync(p => p.Id == playerId, cancel);

        if (player.Team == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "You are not in a team"
            });
        }

        var previousTeamId = player.Team.Id;
        player.Team = null;
        await dbContext.SaveChangesAsync(cancel);
        scoringService.RefreshScores(dbContext);
        logger.LogInformation("Player {PlayerId} left team {TeamId}", playerId, previousTeamId);
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