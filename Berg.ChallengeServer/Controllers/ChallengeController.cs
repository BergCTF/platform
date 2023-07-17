using System.Text.Json.Serialization;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class ChallengeController : ControllerBase
{
    private readonly CtfConfig _ctfConfig;
    private readonly ChallengeService _challengeService;
    private readonly ScoringService _scoringService;
    private readonly PlayerService _playerService;

    public ChallengeController(
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
    public CtfInfo GetCtfInfo()
    {
        return new CtfInfo
        {
            Start = _ctfConfig.Start,
            End = _ctfConfig.End,
            Teams = _ctfConfig.Teams
        };
    }
    
    [HttpGet]
    [Route("/api/v1/challenges")]
    public List<Challenge> GetChallenges()
    {
        var utcNow = DateTime.UtcNow;
        if (_ctfConfig.Start > utcNow)
            throw new ArgumentException("CTF has not started yet");
        return _challengeService.GetChallenges().Select(c =>
        {
            c.Value = _scoringService.GetChallengeValue(c.Name);
            c.TeamSolves = _scoringService.GetChallengeTeamSolves(c.Name);
            c.PlayerSolves = _scoringService.GetChallengePlayerSolves(c.Name);
            return c;
        }).ToList();
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/challengeInstance/status")]
    public async Task<ChallengeInstanceStatus> GetChallengeInstance(CancellationToken cancel)
    {
        var playerId = _playerService.GetPlayer(User).Id;
        return await _challengeService.GetChallengeInstance(playerId, cancel);
    }
    
    public class ChallengeStartRequest
    {
        [JsonPropertyName("challenge")]
        public string? Challenge { get; set; }
    }
    
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/challengeInstance/start")]
    public async Task<ChallengeInstanceStatus> StartChallengeInstance([FromBody] ChallengeStartRequest startRequest,
        CancellationToken cancel)
    {
        var challenge = startRequest.Challenge;
        if (challenge == null)
            throw new ArgumentException("Challenge can't be null");
        
        var playerId = _playerService.GetPlayer(User).Id;
        return await _challengeService.StartChallengeInstance(playerId, challenge, cancel);
    }
    
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/challengeInstance/stop")]
    public async Task<ChallengeInstanceStatus> StopChallengeInstance(CancellationToken cancel)
    {
        var playerId = _playerService.GetPlayer(User).Id;
        return await _challengeService.StopChallengeInstance(playerId, cancel);
    }

}