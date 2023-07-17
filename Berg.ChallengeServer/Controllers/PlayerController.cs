using System.Text.Json.Serialization;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class PlayerController : ControllerBase
{
    private readonly CtfConfig _ctfConfig;
    private readonly ChallengeService _challengeService;
    private readonly PlayerService _playerService;
    
    public PlayerController(
        CtfConfig ctfConfig,
        ChallengeService challengeService,
        PlayerService playerService)
    {
        _ctfConfig = ctfConfig;
        _challengeService = challengeService;
        _playerService = playerService;
    }
    
    [HttpGet]
    [Route("/api/v1/login")]
    public IActionResult Login(CancellationToken cancel)
    {
        return Challenge(new AuthenticationProperties { RedirectUri = "/" });
    }
    
    [HttpGet]
    [Route("/api/v1/logout")]
    public IActionResult Logout(Guid? playerId, CancellationToken cancel)
    {
        return SignOut(new AuthenticationProperties { RedirectUri = "/" });
    }
    
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/self")]
    public async Task<PlayerSelf> GetPlayerSelf(CancellationToken cancel)
    {
        if (User.Identity?.IsAuthenticated ?? false)
            return new PlayerSelf();
        
        var player = _playerService.GetPlayer(User);
        return new PlayerSelf
        {
            Player = new Shared.Player()
            {
                Id = player.Id,
                Name = player.Name,
                TeamId = player.TeamId,
                DiscordId = player.DiscordId
            },
            ChallengeInstance = await _challengeService.GetChallengeInstance(player.Id, cancel)
        };
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
        var utcNow = DateTime.UtcNow;
        if (_ctfConfig.Start > utcNow)
            throw new ArgumentException("CTF has not started yet");
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