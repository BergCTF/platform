using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Challenge = Berg.Shared.Challenge;

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
        var now = DateTime.UtcNow;
        var player = (User.Identity?.IsAuthenticated ?? false) ? _playerService.GetPlayer(User) : null;
        if (_ctfConfig.Start <= now)
        {
            ctf.Challenges = _challengeService.GetChallenges()
                .Select(c => ToChallenge(c, player?.Id, player?.TeamId))
                .ToList()
                .GroupBy(c => c.Categories.FirstOrDefault() ?? "misc")
                .ToDictionary(c => c.Key, c => c
                    .OrderBy(c2 => c2.Value)
                    .ThenBy(c2 => c2.Name)
                    .ToList());
        }
        return ctf;
    }
    
    private Challenge ToChallenge(V1Challenge c, Guid? playerId, Guid? teamId)
    {
        return new Challenge
        {
            Name = c.Name(),
            Author = c.Spec.Author,
            Description = c.Spec.Description,
            Difficulty = c.Spec.Difficulty,
            Categories = c.Spec.Categories,
            Instantiatable = c.Spec.Containers?.Any() ?? false,
            SolvedByPlayer = _scoringService.HasPlayerSolvedChallenge(playerId, c.Name()),
            SolvedByTeam = _scoringService.HasTeamSolvedChallenge(teamId, c.Name()),
            TeamSolves = _scoringService.GetChallengeTeamSolves(c.Name()),
            PlayerSolves = _scoringService.GetChallengePlayerSolves(c.Name()),
            Attachments = c.Spec.Attachments?.Select(a => new Attachment
            {
                FileName = a.FileName,
                DownloadUrl = a.DownloadUrl,
            }).ToList() ?? new List<Attachment>(),
        };
    }
    
    [HttpGet]
    [Route("/api/v1/players")]
    public async Task<List<Shared.Player>> ListPlayers(CancellationToken cancel)
    {
        return await _dbContext.Players.Select(t => new Shared.Player
        {
            Id = t.Id,
            Name = t.Name,
            TeamId = t.TeamId,
            DiscordId = t.DiscordId
        }).ToListAsync(cancel);
    }

}