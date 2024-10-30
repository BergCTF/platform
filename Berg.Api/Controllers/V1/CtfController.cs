using System.Security.Claims;
using Berg.Api.Configuration;
using Berg.Api.CustomResources.Berg;
using Berg.Api.Db;
using Berg.Api.Services;
using Berg.Api.Models.V1;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Challenge = Berg.Api.Models.V1.Challenge;

namespace Berg.Api.Controllers.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
public class CtfController : ControllerBase
{
    private readonly CtfConfig _ctfConfig;
    private readonly IChallengeService _challengeService;
    private readonly ScoringService _scoringService;
    private readonly BergDbContext _dbContext;

    public CtfController(
        CtfConfig ctfConfig,
        ScoringService scoringService,
        IChallengeService challengeService,
        BergDbContext dbContext)
    {
        _ctfConfig = ctfConfig;
        _scoringService = scoringService;
        _challengeService = challengeService;
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
            ServerTime = DateTime.Now,
            FreezeStart = _ctfConfig.Scoring.FreezeStart,
            FreezeEnd = _ctfConfig.Scoring.FreezeEnd,
            Teams = _ctfConfig.Teams
        };
        var now = DateTime.UtcNow;
        Db.Player? player = null;
        if (User.Identity?.IsAuthenticated ?? false) {
            var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
            player = _dbContext.Players.Single(p => p.Id == playerId);
        }
        if (_ctfConfig.Start <= now)
        {
            ctf.Challenges = _challengeService.GetChallenges()
                .Select(c => ToChallenge(c, player?.Id, player?.TeamId))
                .ToList()
                .GroupBy(c => c.Categories.FirstOrDefault() ?? "misc")
                .ToDictionary(c => c.Key, c => c
                    .OrderBy(c2 => c2.Value)
                    .ThenBy(c2 => DifficultyToNumber(c2.Difficulty))
                    .ThenBy(c2 => c2.Name)
                    .ToList());
        }
        return ctf;
    }

    private static int DifficultyToNumber(string val)
    {
        var mappings = new Dictionary<string, int>
        {
            { "baby", 1 },
            { "easy", 2 },
            { "medium", 3 },
            { "hard", 4 },
            { "leet", 5 },
        };
        return mappings.TryGetValue(val, out var result) ? result : 6;
    }

    private Challenge ToChallenge(V1Challenge c, Guid? playerId, Guid? teamId)
    {
        var challengeName = c.Name();
        return new Challenge
        {
            Name = challengeName,
            Author = c.Spec.Author,
            Description = c.Spec.Description,
            Difficulty = c.Spec.Difficulty,
            FlagFormat = c.Spec.FlagFormat,
            Categories = c.Spec.Categories,
            Instantiatable = c.Spec.Containers?.Any() ?? false,
            Value = _scoringService.GetChallengeValue(challengeName),
            SolvedByPlayer = _scoringService.HasPlayerSolvedChallenge(playerId, challengeName),
            SolvedByTeam = _scoringService.HasTeamSolvedChallenge(teamId, challengeName),
            TeamSolves = _scoringService.GetChallengeTeamSolves(challengeName),
            PlayerSolves = _scoringService.GetChallengePlayerSolves(challengeName),
            Attachments = c.Spec.Attachments?.Select(a => new Attachment
            {
                FileName = a.FileName,
                DownloadUrl = a.DownloadUrl,
            }).ToList() ?? new List<Attachment>(),
        };
    }
}