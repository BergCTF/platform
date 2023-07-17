using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class PlayerController : ControllerBase
{
    private readonly BergDbContext _dbContext;
    private readonly ScoringService _scoringService;
    private readonly PlayerService _playerService;
    
    public PlayerController(
        BergDbContext dbContext,
        ScoringService scoringService,
        PlayerService playerService)
    {
        _dbContext = dbContext;
        _scoringService = scoringService;
        _playerService = playerService;
    }
    
    [HttpGet]
    [Route("/api/v1/players")]
    public async Task<List<Shared.Player>> ListPlayers(CancellationToken cancel)
    {
        return await _dbContext.Players.Select(t => new Shared.Player
        {
            Id = t.Id,
            Name = t.Name
        }).ToListAsync(cancel);
    }
    
    [HttpGet]
    [Route("/api/v1/players/ranking")]
    public PlayerRanking GetPlayerRanking(Guid? playerId, CancellationToken cancel)
    {
        var requestedPlayerId = playerId ?? _playerService.GetPlayer(User).Id;

        var solves = _scoringService.GetPlayerSolves(requestedPlayerId);
        return new PlayerRanking
        {
            PlayerId = requestedPlayerId,
            Score = _scoringService.GetPlayerScore(requestedPlayerId),
            Solves = solves,
            LastSolve = solves.Count > 0 ? solves.Select(s => s.SolvedAt).Max() : null
        };
    }
}