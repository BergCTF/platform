using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Challenge = Berg.Shared.Challenge;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class CtfController : ControllerBase
{
    private readonly CtfConfig _ctfConfig;
    private readonly ChallengeService _challengeService;
    private readonly ScoringService _scoringService;
    private readonly PlayerService _playerService;

    public CtfController(
        CtfConfig ctfConfig,
        ScoringService scoringService,
        ChallengeService challengeService,
        PlayerService playerService)
    {
        _ctfConfig = ctfConfig;
        _scoringService = scoringService;
        _challengeService = challengeService;
        _playerService = playerService;
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
        var player = (User.Identity?.IsAuthenticated ?? false) ? _playerService.GetPlayer(User) : null;
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