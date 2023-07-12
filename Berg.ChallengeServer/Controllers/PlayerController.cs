using System.Security.Cryptography;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Player = Berg.ChallengeServer.Db.Player;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class PlayerController : Controller
{
    private readonly ILogger<PlayerController> _logger;
    private readonly BergDbContext _dbContext;
    private readonly ScoringService _scoringService;
    
    public PlayerController(
        ILogger<PlayerController> logger,
        BergDbContext dbContext,
        ScoringService scoringService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _scoringService = scoringService;
    }
    
    [HttpGet]
    [Route("/api/v1/players")]
    public async Task<List<Shared.Player>> ListTeams(CancellationToken cancel)
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
        var requestedPlayerId = playerId ?? GetPlayerId();

        var solves = _scoringService.GetPlayerSolves(requestedPlayerId);
        return new PlayerRanking
        {
            PlayerId = requestedPlayerId,
            Score = _scoringService.GetPlayerScore(requestedPlayerId),
            Solves = solves,
            LastSolve = solves.Count > 0 ? solves.Select(s => s.SolvedAt).Max() : null
        };
    }
    
    private static Guid GetPlayerId()
    {
        return Guid.Empty;
    }
}