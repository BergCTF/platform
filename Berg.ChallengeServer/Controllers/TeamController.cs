using System.Security.Cryptography;
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
    
    public TeamController(
        ILogger<TeamController> logger,
        BergDbContext dbContext,
        PlayerService playerService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _playerService = playerService;
    }
    
    [HttpGet]
    [Route("/api/v1/teams")]
    public async Task<List<Shared.Team>> ListTeams(CancellationToken cancel)
    {
        return await _dbContext.Teams.Select(t => new Shared.Team
        {
            Id = t.Id,
            Name = t.Name,
            JoinToken = null,
            Players = t.Players.Select(p => p.Id).ToList()
        }).ToListAsync(cancel);
    }
    
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/teams/create")]
    public async Task<Shared.Team> CreateTeam(Shared.Team team, CancellationToken cancel)
    {
        var playerId = _playerService.GetPlayer(User).Id;
        var player = await _dbContext.Players
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == playerId, cancel);
        if (player == null)
            throw new ArgumentException("Invalid player");

        if (player.Team != null)
            throw new ArgumentException("Player is already in a team");

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
        
        _logger.LogInformation("Player {} created team: {}", playerId, dbTeam.Id);

        team.Id = dbTeam.Id;
        team.JoinToken = dbTeam.JoinToken;
        team.Players = new List<Guid> { playerId };
        return team;
    }
    
    [HttpGet]
    [Route("/api/v1/teams/info")]
    public async Task<Shared.Team?> GetTeam(Guid? teamId, CancellationToken cancel)
    {
        Team? dbTeam;
        if (teamId == null)
        {
            var playerId = _playerService.GetPlayer(User).Id;
            var player = await _dbContext.Players
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.Id == playerId, cancel);
            if (player == null)
                throw new ArgumentException("Invalid player");
            
            dbTeam = player.Team;
        }
        else
        {
            dbTeam = await _dbContext.Teams
                .Include(t => t.Players)
                .FirstOrDefaultAsync(t => t.Id == teamId, cancel);
            if (dbTeam == null)
                throw new ArgumentException("Invalid team id");
        }

        if (dbTeam == null)
            return null;
        
        var playerIds = dbTeam.Players.Select(p => p.Id).ToList();
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
    [Route("/api/v1/teams/join")]
    public async Task<Shared.Team> JoinTeam(string joinToken, CancellationToken cancel)
    {
        joinToken = joinToken.Trim();
        
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