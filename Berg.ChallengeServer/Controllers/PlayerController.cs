using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Player = Berg.Shared.Player;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class PlayerController : ControllerBase
{
    private readonly ChallengeService _challengeService;
    private readonly CtfConfig _ctfConfig;
    private readonly PlayerService _playerService;
    private readonly BergDbContext _dbContext;
    
    public PlayerController(
        ChallengeService challengeService,
        CtfConfig ctfConfig,
        PlayerService playerService,
        BergDbContext dbContext)
    {
        _challengeService = challengeService;
        _ctfConfig = ctfConfig;
        _playerService = playerService;
        _dbContext = dbContext;
    }
    
    [HttpGet]
    [Route("/api/v1/players")]
    public async Task<List<Player>> ListPlayers(CancellationToken cancel)
    {
        var publicCustomAttributes = _ctfConfig.PlayerAttributes?
            .Where(a => a.Public).Select(a => a.Name)
            .ToHashSet() ?? new HashSet<string>();
        var players = await _dbContext.Players.ToListAsync(cancel);
        return players.Select(p => new Player
        {
            Id = p.Id,
            Name = p.Name,
            TeamId = p.TeamId,
            DiscordId = p.DiscordId,
            Attributes = p.Attributes?
                .Where(a => publicCustomAttributes.Contains(a.Name))
                .ToDictionary(a => a.Name, a => a.Value) ?? new Dictionary<string, string>(),
            RequiredAttributes = new List<Shared.PlayerAttribute>(),
        }).ToList();
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("/api/v1/self")]
    public async Task<PlayerSelf> GetPlayerSelf(CancellationToken cancel)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return new PlayerSelf();
        
        var player = _playerService.GetPlayer(User);
        var requiredAttributes = _ctfConfig.PlayerAttributes?
            .Where(a => a.Required).ToHashSet() ?? new HashSet<Shared.PlayerAttribute>();
        return new PlayerSelf
        {
            Player = new Player
            {
                Id = player.Id,
                Name = player.Name,
                TeamId = player.TeamId,
                DiscordId = player.DiscordId,
                Attributes = player.Attributes.ToDictionary(a => a.Name, a => a.Value),
                RequiredAttributes = requiredAttributes
                    .Where(a => player.Attributes.All(pa => pa.Name != a.Name))
                    .ToList()
            },
            ChallengeInstance = await _challengeService.GetChallengeInstance(player.Id, cancel)
        };
    }

    public class PlayerUpdateRequest
    {
        public Dictionary<string, string> Attributes { get; set; } = new();
    }
    
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/self")]
    public void UpdatePlayerSelf(PlayerUpdateRequest playerUpdate)
    {
        var player = _playerService.GetPlayer(User);
        var configAttributesByName = _ctfConfig.PlayerAttributes?
            .ToDictionary(a => a.Name) ?? new Dictionary<string, Shared.PlayerAttribute>();
        foreach (var attr in playerUpdate.Attributes)
        {
            if(!configAttributesByName.TryGetValue(attr.Key, out var configAttr))
                throw new ArgumentException($"Invalid attribute name: {attr.Key}");
            if (!configAttr.Values.Contains(attr.Value))
                throw new ArgumentException($"Invalid attribute value: {attr.Value}");
        }
        _playerService.UpdatePlayerAttributes(player, playerUpdate.Attributes);
    }
}
