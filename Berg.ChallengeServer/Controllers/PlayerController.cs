using System.Text.Json.Serialization;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class PlayerController : ControllerBase
{
    private readonly ChallengeService _challengeService;
    private readonly PlayerService _playerService;
    
    public PlayerController(
        ChallengeService challengeService,
        PlayerService playerService)
    {
        _challengeService = challengeService;
        _playerService = playerService;
    }
    
    [HttpGet]
    [Route("/api/v1/login")]
    public IActionResult Login(CancellationToken cancel, string redirectUri = "/")
    {
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri });
    }
    
    [HttpGet]
    [Route("/api/v1/logout")]
    public IActionResult Logout(Guid? playerId, CancellationToken cancel, string redirectUri = "/")
    {
        return SignOut(new AuthenticationProperties { RedirectUri = redirectUri });
    }
    
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("/api/v1/self")]
    public async Task<PlayerSelf> GetPlayerSelf(CancellationToken cancel)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return new PlayerSelf();
        
        var player = _playerService.GetPlayer(User);
        return new PlayerSelf
        {
            Player = new Player
            {
                Id = player.Id,
                Name = player.Name,
                TeamId = player.TeamId,
                DiscordId = player.DiscordId,
                Labels = player.Labels
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
    public async Task<ChallengeInstanceStatus?> StartChallengeInstance([FromBody] ChallengeStartRequest startRequest,
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