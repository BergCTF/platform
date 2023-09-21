using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class TeamController : ControllerBase
{
    private readonly ILogger<TeamController> _logger;
    private readonly BergDbContext _dbContext;
    private readonly PlayerService _playerService;
    private readonly ScoringService _scoringService;
    
    public TeamController(
        ILogger<TeamController> logger,
        BergDbContext dbContext,
        PlayerService playerService,
        ScoringService scoringService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _playerService = playerService;
        _scoringService = scoringService;
    }
    
    [HttpGet]
    [Route("/api/v1/teams")]
    public async Task<List<Shared.Team>> ListTeams(CancellationToken cancel)
    {
        var player = (User.Identity?.IsAuthenticated ?? false) ? _playerService.GetPlayer(User) : null;
        var teamId = player?.TeamId;
        return (await _dbContext.Teams.Select(t => new Shared.Team
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
    public async Task<Shared.Team> CreateTeam([FromBody] TeamCreateRequest team, CancellationToken cancel)
    {
        var playerId = _playerService.GetPlayer(User).Id;
        var player = await _dbContext.Players
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == playerId, cancel);
        if (player == null)
            throw new ArgumentException("Invalid player");
        if (team.Name == null)
            throw new ArgumentException("Team name must be set");
        if (team.Name.Length > 100)
            throw new ArgumentException("Team name is too long. Thank coderion for this");
        if (!team.Name.All(char.IsAscii))
            throw new ArgumentException("Team name must be ascii-only");
        
        if (player.Team != null)
            throw new ArgumentException("Player is already in a team");

        if(_dbContext.Teams.Any(t => t.Name == team.Name))
            throw new ArgumentException("Name is already taken");
            
        // Create team
        var dbTeam = new Team
        {
            Id = Guid.NewGuid(),
            Name = team.Name,
            JoinToken = CreateJoinToken()
        };
        await _dbContext.Teams.AddAsync(dbTeam, cancel);
        
        // Add player to team
        player.Team = dbTeam;
        await _dbContext.SaveChangesAsync(cancel);
        _playerService.RefreshPlayerInfo(_dbContext);
        _scoringService.RefreshScores(_dbContext);
        _logger.LogInformation("Player {} created team: {}", playerId, dbTeam.Id);

        return new Shared.Team
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
    public async Task<Shared.Team> JoinTeam([FromBody] JoinTeamRequest req, CancellationToken cancel)
    {
        if(req.JoinToken == null)
            throw new ArgumentException("Join token can't be null");
        var joinToken = req.JoinToken.Trim();
        
        var playerId = _playerService.GetPlayer(User).Id;
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
        _playerService.RefreshPlayerInfo(_dbContext);
        _scoringService.RefreshScores(_dbContext);
        _logger.LogInformation("Player {} joined team: {}", playerId, dbTeam.Id);
        
        // Add our new player to the list of players as we fetched the db information
        // before assigning the user to the team.
        var playerIds = dbTeam.Players.Select(p => p.Id).ToList();
        playerIds.Add(player.Id);
        
        var team = new Shared.Team
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
        var playerId = _playerService.GetPlayer(User).Id;
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
        _playerService.RefreshPlayerInfo(_dbContext);
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