using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Team = Berg.Api.Models.V1.Team;

namespace Berg.Api.Controllers.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
public class TeamController : ControllerBase
{
    private readonly ILogger<TeamController> _logger;
    private readonly BergDbContext _dbContext;
    private readonly ScoringService _scoringService;
    private readonly CtfConfig _ctfConfig;

    public TeamController(
        ILogger<TeamController> logger,
        BergDbContext dbContext,
        ScoringService scoringService,
        CtfConfig ctfConfig)
    {
        _logger = logger;
        _dbContext = dbContext;
        _scoringService = scoringService;
        _ctfConfig = ctfConfig;
    }

    [HttpGet]
    [Route("/api/v1/teams")]
    public async Task<List<Team>> ListTeams(CancellationToken cancel)
    {
        Db.Player? player = null;
        if (User.Identity?.IsAuthenticated ?? false) {
            var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
            player = _dbContext.Players.Single(p => p.Id == playerId);
        }
        var teamId = player?.TeamId;
        return (await _dbContext.Teams.Select(t => new Team
        {
            Id = t.Id,
            Name = t.Name,
            JoinToken = t.JoinToken,
            Players = t.Players.Select(p => p.Id).ToList()
        }).ToListAsync(cancel))
            .Select(t =>
            {
                if (teamId == null || t.Id != teamId.Value)
                    t.JoinToken = null;
                return t;
            })
            .ToList();
    }

    public class TeamCreateRequest
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/teams/create")]
    public async Task<Team> CreateTeam([FromBody] TeamCreateRequest team, CancellationToken cancel)
    {
        if(!_ctfConfig.Teams)
            throw new ArgumentException("Teams not enabled");

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await _dbContext.Players
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == playerId, cancel);
        if (player == null)
            throw new ArgumentException("Invalid player");
        if (team.Name == null)
            throw new ArgumentException("Team name must be set");
        if (team.Name.Length > 128)
            throw new ArgumentException("Team name is too long. Thank coderion for this");
        if (!team.Name.All(char.IsAscii))
            throw new ArgumentException("Team name must be ascii-only");

        if (player.Team != null)
            throw new ArgumentException("Player is already in a team");

        if(_dbContext.Teams.Any(t => t.Name == team.Name))
            throw new ArgumentException("Name is already taken");

        // Create team
        var dbTeam = new Db.Team
        {
            Id = Guid.NewGuid(),
            Name = team.Name,
            JoinToken = CreateJoinToken()
        };
        await _dbContext.Teams.AddAsync(dbTeam, cancel);

        // Add player to team
        player.Team = dbTeam;
        await _dbContext.SaveChangesAsync(cancel);
        _scoringService.RefreshScores(_dbContext);
        _logger.LogInformation("Player {} created team: {}", playerId, dbTeam.Id);

        return new Team
        {
            Id = dbTeam.Id,
            JoinToken = dbTeam.JoinToken,
            Players = new List<Guid> { playerId }
        };
    }

    public class JoinTeamRequest
    {
        [JsonPropertyName("joinToken")]
        public string? JoinToken { get; set; }
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/teams/join")]
    public async Task<Team> JoinTeam([FromBody] JoinTeamRequest req, CancellationToken cancel)
    {
        if(!_ctfConfig.Teams)
            throw new ArgumentException("Teams not enabled");

        if(req.JoinToken == null)
            throw new ArgumentException("Join token can't be null");
        var joinToken = req.JoinToken.Trim();

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await _dbContext.Players
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == playerId, cancel);
        if (player == null)
            throw new ArgumentException("Invalid player");

        if (player.Team != null)
            throw new ArgumentException("Player is already in a team");

        var dbTeam = await _dbContext.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.JoinToken == joinToken, cancel);
        if (dbTeam == null)
        {
            _logger.LogWarning("Player {} tried to use an invalid team join token: {}", playerId, joinToken);
            throw new ArgumentException("Invalid join token");
        }

        // Assign the player to the team
        player.Team = dbTeam;
        await _dbContext.SaveChangesAsync(cancel);
        _scoringService.RefreshScores(_dbContext);
        _logger.LogInformation("Player {} joined team: {}", playerId, dbTeam.Id);

        // Add our new player to the list of players as we fetched the db information
        // before assigning the user to the team.
        var playerIds = dbTeam.Players.Select(p => p.Id).ToList();
        playerIds.Add(player.Id);

        var team = new Team
        {
            Id = dbTeam.Id,
            Name = dbTeam.Name,
            JoinToken = dbTeam.JoinToken,
            Players = playerIds,
        };

        return team;
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/teams/leave")]
    public async Task LeaveTeam(CancellationToken cancel)
    {
        if(!_ctfConfig.Teams)
            throw new ArgumentException("Teams not enabled");

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await _dbContext.Players
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == playerId, cancel);
        if (player == null)
            throw new ArgumentException("Invalid player");

        if (player.Team == null)
            throw new ArgumentException("Player is not in a team");

        var previousTeamId = player.Team.Id;
        player.Team = null;
        await _dbContext.SaveChangesAsync(cancel);
        _scoringService.RefreshScores(_dbContext);
        _logger.LogInformation("Player {} left team {}", playerId, previousTeamId);
    }

    private static readonly RandomNumberGenerator Random = RandomNumberGenerator.Create();

    private static string CreateJoinToken()
    {
        var buf = new byte[32];
        Random.GetBytes(buf);
        return Convert.ToHexString(buf);
    }
}