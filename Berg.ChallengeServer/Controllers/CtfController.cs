using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class CtfController : ControllerBase
{
    private readonly CtfConfig _ctfConfig;
    private readonly ChallengeService _challengeService;
    private readonly ScoringService _scoringService;
    private readonly PlayerService _playerService;
    private readonly BergDbContext _dbContext;

    public CtfController(
        CtfConfig ctfConfig,
        ScoringService scoringService,
        ChallengeService challengeService,
        PlayerService playerService,
        BergDbContext dbContext)
    {
        _ctfConfig = ctfConfig;
        _scoringService = scoringService;
        _challengeService = challengeService;
        _playerService = playerService;
        _dbContext = dbContext;
    }

    [HttpGet]
    [Route("/api/v1/ctf")]
    public CtfChallenges GetCtfChallenges()
    {
        var ctf = new CtfChallenges
        {
            Start = _ctfConfig.Start,
            End = _ctfConfig.End,
            Teams = _ctfConfig.Teams
        };
        var utcNow = DateTime.UtcNow;
        var player = (User.Identity?.IsAuthenticated ?? false) ? _playerService.GetPlayer(User) : null;
        if (_ctfConfig.Start <= utcNow)
        {
            ctf.Challenges = _challengeService.GetChallenges(player?.Id, player?.TeamId).Select(c =>
            {
                c.Value = _scoringService.GetChallengeValue(c.Name);
                c.TeamSolves = _scoringService.GetChallengeTeamSolves(c.Name);
                c.PlayerSolves = _scoringService.GetChallengePlayerSolves(c.Name);
                return c;
            }).ToList()
                .GroupBy(c => c.Categories.FirstOrDefault() ?? "misc")
                .ToDictionary(c => c.Key, c => c
                    .OrderBy(c2 => c2.Value)
                    .ThenBy(c2 => c2.Name)
                    .ToList());
        }
        return ctf;
    }
    
    [HttpGet]
    [Route("/api/v1/players")]
    public async Task<List<Shared.Player>> ListPlayers(CancellationToken cancel)
    {
        return await _dbContext.Players.Select(t => new Shared.Player
        {
            Id = t.Id,
            Name = t.Name,
            DiscordId = t.DiscordId
        }).ToListAsync(cancel);
    }

}