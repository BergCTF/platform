using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Models.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using System.Security.Claims;
using System.Security.Cryptography;
using Player = Berg.Api.Models.V2.Player;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class PlayerController(CtfConfig ctfConfig, BergDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [Route("/api/v2/players")]
    public async Task<List<Player>> ListPlayers(CancellationToken cancel)
    {
        var publicCustomAttributes = GetPublicCustomAttributes();
        var players = await dbContext.Players
            .Include(p => p.Attributes)
            .ToListAsync(cancel);
        return players.Select(p => ToModelPlayer(p, publicCustomAttributes)).ToList();
    }

    [HttpGet]
    [Route("/api/v2/players/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Player>> GetPlayer(Guid id, CancellationToken cancel)
    {
        var publicCustomAttributes = GetPublicCustomAttributes();
        var player = await dbContext.Players
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Id == id, cancel);
        if (player == null)
            return NotFound();
        return ToModelPlayer(player, publicCustomAttributes);
    }

    [HttpGet]
    [Authorize]
    [Route("/api/v2/players/me")]
    public async Task<ActionResult<Me>> GetMe(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await dbContext.Players
            .Include(p => p.Attributes)
            .SingleAsync(p => p.Id == playerId, cancel);
        return Ok(new Me
        {
            Id = player.Id,
            Name = player.Name,
            FederatedId = player.FederatedId,
            Attributes = player.Attributes.ToDictionary(a => a.Name, a => a.Value),
            ApiKeyPlaceholder = player.ApiKeyPlaceholder,
        });
    }

    public class AttributesUpdateRequest
    {
        public Dictionary<string, string> Attributes { get; set; } = [];
    }

    [HttpPatch]
    [Authorize]
    [Route("/api/v2/players/me")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult UpdatePlayerAttributes(AttributesUpdateRequest attrUpdate)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = dbContext.Players
            .Include(p => p.Attributes)
            .Single(p => p.Id == playerId);
        var configAttributesByName = ctfConfig.PlayerAttributes?
            .ToDictionary(a => a.Name) ?? [];
        foreach (var attr in attrUpdate.Attributes)
        {
            if(!configAttributesByName.TryGetValue(attr.Key, out var configAttr))
                return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = $"Invalid attribute name: {attr.Key}"});
            if (!configAttr.Values.Contains(attr.Value))
                return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = $"Invalid attribute value: {attr.Value}"});
        }

        foreach (var pair in attrUpdate.Attributes)
        {
            var existingAttr = player.Attributes.FirstOrDefault(a => a.Name == pair.Key);
            if (existingAttr != null)
            {
                existingAttr.Value = pair.Value;
            }
            else
            {
                player.Attributes.Add(new PlayerAttribute()
                {
                    Player = player,
                    Name = pair.Key,
                    Value = pair.Value
                });
            }
        }
        dbContext.SaveChanges();
        return Ok();
    }


    [HttpDelete]
    [Authorize]
    [Route("/api/v2/players/me")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult DeleteMe()
    {
        var loginType = User.FindFirstValue(Constants.Claims.LoginType)!;
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);

        if (loginType != Constants.LoginTypes.Federation)
        {
            return BadRequest(new ProblemDetails {
                Title = "Bad Request",
                Detail = "Can't delete your account with a token obtained through api key authentication."
            });
        }

        var player = dbContext.Players.First(p => p.Id == playerId);
        dbContext.Players.Remove(player);
        dbContext.SaveChanges();

        return SignOut();
    }

    [HttpDelete]
    [Authorize]
    [Route("/api/v2/players/me/api-key")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<string> ResetApiKey()
    {
        var loginType = User.FindFirstValue(Constants.Claims.LoginType)!;

        if (loginType != Constants.LoginTypes.Federation)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Can't reset api key with a token obtained through api key authentication.",
            });
        }

        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var newApiKey = RandomNumberGenerator.GetHexString(64, true);
        var apiKeyHash = Helpers.GetApiKeyHash(newApiKey, playerId);

        var player = dbContext.Players.Single(p => p.Id == playerId);
        player.ApiKeyPlaceholder = newApiKey[..4] + new string('*', newApiKey.Length - 4);
        player.ApiKeyHash = apiKeyHash;
        dbContext.SaveChanges();

        return Ok(newApiKey);
    }

    private HashSet<string> GetPublicCustomAttributes()
    {
        return ctfConfig.PlayerAttributes?
            .Where(a => a.Public)
            .Select(a => a.Name)
            .ToHashSet() ?? [];
    }

    private static Player ToModelPlayer(Db.Player player, HashSet<string> publicCustomAttributeNames)
    {
        return new Player
        {
            Id = player.Id,
            Name = player.Name,
            Attributes = player.Attributes
                .Where(a => publicCustomAttributeNames.Contains(a.Name))
                .ToDictionary(a => a.Name, a => a.Value),
        };
    }
}