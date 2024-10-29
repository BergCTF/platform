using System.Security.Claims;
using System.Security.Cryptography;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Player = Berg.Shared.Player;

namespace Berg.Api.Controllers;

[ApiController]
public class PlayerController : ControllerBase
{
    private readonly IChallengeService _challengeService;
    private readonly CtfConfig _ctfConfig;
    private readonly BergDbContext _dbContext;

    public PlayerController(
        IChallengeService challengeService,
        CtfConfig ctfConfig,
        BergDbContext dbContext)
    {
        _challengeService = challengeService;
        _ctfConfig = ctfConfig;
        _dbContext = dbContext;
    }

    [HttpGet]
    [Route("/api/v1/players")]
    public async Task<List<Player>> ListPlayers(CancellationToken cancel)
    {
        var publicCustomAttributes = _ctfConfig.PlayerAttributes?
            .Where(a => a.Public).Select(a => a.Name)
            .ToHashSet() ?? new HashSet<string>();
        var players = await _dbContext.Players
            .Include(p => p.Attributes)
            .ToListAsync(cancel);
        return players.Select(p => new Player
        {
            Id = p.Id,
            Name = p.Name,
            TeamId = p.TeamId,
            FederatedId = "", // No need to leak federated ids to the public
            Attributes = p.Attributes
                .Where(a => publicCustomAttributes.Contains(a.Name))
                .ToDictionary(a => a.Name, a => a.Value),
            RequiredAttributes = new List<Shared.PlayerAttribute>(),
        }).ToList();
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("/api/v1/self")]
    public async Task<PlayerSelf> GetPlayerSelf(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = _dbContext.Players
            .Include(p => p.Attributes)
            .Single(p => p.Id == playerId);
        var requiredAttributes = _ctfConfig.PlayerAttributes?
            .Where(a => a.Required).ToHashSet() ?? new HashSet<Shared.PlayerAttribute>();
        return new PlayerSelf
        {
            Player = new Player
            {
                Id = player.Id,
                Name = player.Name,
                TeamId = player.TeamId,
                FederatedId = player.FederatedId,
                Attributes = player.Attributes.ToDictionary(a => a.Name, a => a.Value),
                RequiredAttributes = requiredAttributes
                    .Where(a => player.Attributes.All(pa => pa.Name != a.Name))
                    .ToList()
            },
            ApiKeyPlaceholder = player.ApiKeyPlaceholder,
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
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = _dbContext.Players.Single(p => p.Id == playerId);
        var configAttributesByName = _ctfConfig.PlayerAttributes?
            .ToDictionary(a => a.Name) ?? [];
        foreach (var attr in playerUpdate.Attributes)
        {
            if(attr.Key.Length > 128)
                throw new ArgumentException($"Attribute name too long (max 128): {attr.Key.Length}");
            if(attr.Value.Length > 128)
                throw new ArgumentException($"Attribute value too long (max 128): {attr.Value.Length}");
            if(!configAttributesByName.TryGetValue(attr.Key, out var configAttr))
                throw new ArgumentException($"Invalid attribute name: {attr.Key}");
            if (!configAttr.Values.Contains(attr.Value))
                throw new ArgumentException($"Invalid attribute value: {attr.Value}");
        }

        foreach (var pair in playerUpdate.Attributes)
        {
            var existingAttr = player.Attributes.FirstOrDefault(a => a.Name == pair.Key);
            if (existingAttr != null)
            {
                existingAttr.Value = pair.Value;
            }
            else
            {
                player.Attributes.Add(new Db.PlayerAttribute()
                {
                    Player = player,
                    Name = pair.Key,
                    Value = pair.Value
                });
            }
        }
        _dbContext.SaveChanges();
    }

    [HttpDelete]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/self/api-key")]
    public ActionResult<string> DeleteApiKey()
    {
        var loginType = User.FindFirstValue(Constants.Claims.LoginType)!;

        if (loginType != Constants.LoginTypes.Federation)
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid login type",
                Detail = "Can't reset api key with a token obtained through api key authentication.",
            });

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var newApiKey = RandomNumberGenerator.GetHexString(64, true);
        var apiKeyHash = Helpers.GetApiKeyHash(newApiKey, playerId);

        var player = _dbContext.Players.Single(p => p.Id == playerId);
        player.ApiKeyPlaceholder = newApiKey[..4] + new string('*', newApiKey.Length - 4);
        player.ApiKeyHash = apiKeyHash;
        _dbContext.SaveChanges();

        return Ok(newApiKey);
    }

    [HttpDelete]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/self")]
    public IActionResult DeleteSelf()
    {
        var loginType = User.FindFirstValue(Constants.Claims.LoginType)!;
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);

        if (loginType != Constants.LoginTypes.Federation)
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid login type",
                Detail = "Can't delete your account with a token obtained through api key authentication.",
            });

        var player = _dbContext.Players.First(p => p.Id == playerId);
        _dbContext.Players.Remove(player);
        _dbContext.SaveChanges();

        return SignOut();
    }
}
