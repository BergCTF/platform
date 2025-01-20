using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Models.V2;
using Berg.Api.Notifications;
using Berg.Api.Services;
using MediatR;
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
public class PlayerController(CtfConfig ctfConfig,
    BergDbContext dbContext,
    BergMetrics metrics,
    IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Route("/api/v2/players")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [ProducesResponseType(typeof(List<Player>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<Player>>> ListPlayers(CancellationToken cancel)
    {
        var publicCustomAttributes = GetPublicCustomAttributeNames(ctfConfig);
        var players = await dbContext.Players
            .Include(p => p.Attributes)
            .ToListAsync(cancel);
        return players.Select(p => ToModelPlayer(p, publicCustomAttributes)).ToList();
    }

    [HttpGet]
    [Route("/api/v2/players/{id:guid}")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [ProducesResponseType(typeof(Player), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Player>> GetPlayer([FromRoute] Guid id, CancellationToken cancel)
    {
        var publicCustomAttributes = GetPublicCustomAttributeNames(ctfConfig);
        var player = await dbContext.Players
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Id == id, cancel);
        if (player == null)
            return NotFound();
        return ToModelPlayer(player, publicCustomAttributes);
    }

    [HttpGet]
    [Route("/api/v2/players/current")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(CurrentPlayer), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentPlayer>> GetCurrentPlayer(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await dbContext.Players
            .Include(p => p.Attributes)
            .SingleAsync(p => p.Id == playerId, cancel);
        return Ok(new CurrentPlayer
        {
            Id = player.Id,
            Name = player.Name,
            Roles = player.Roles ?? [],
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
    [Route("/api/v2/players/current")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult UpdateCurrentPlayerAttributes(AttributesUpdateRequest attrUpdate)
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
                player.Attributes.Add(new Db.PlayerAttribute()
                {
                    Player = player,
                    Name = pair.Key,
                    Value = pair.Value
                });
            }
        }
        dbContext.SaveChanges();

        var _ = mediator.Publish(new PlayerUpdateNotification
        {
            DbPlayer = player
        });

        return Ok();
    }


    [HttpDelete]
    [Route("/api/v2/players/current")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult DeleteCurrentPlayer()
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

        var player = dbContext.Players
            .First(p => p.Id == playerId);
        dbContext.Players.Remove(player);
        dbContext.SaveChanges();

        var _ = mediator.Publish(new PlayerDeleteNotification
        {
            PlayerId = player.Id,
        });

        if (player.TeamId != null) {
            // Also send a team update if the player was part of a team
            var dbTeam = dbContext.Teams
                .Include(t => t.Players)
                .Single(t => t.Id == player.TeamId);
            var __ = mediator.Publish(new TeamUpdateNotification
            {
                Team = new Models.V2.Team
                {
                    Id = dbTeam.Id,
                    Name = dbTeam.Name,
                    Players = dbTeam.Players
                        .Select(p => p.Id)
                        .ToList(),
                }
            });
        }

        metrics.PlayerDeleted();

        return SignOut();
    }

    [HttpDelete]
    [Route("/api/v2/players/current/api-key")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
        player.ApiKeyPlaceholder = newApiKey[..4] + new string('*', 10);
        player.ApiKeyHash = apiKeyHash;
        dbContext.SaveChanges();

        return Ok(newApiKey);
    }

    internal static HashSet<string> GetPublicCustomAttributeNames(CtfConfig ctfConfig)
    {
        return ctfConfig.PlayerAttributes?
            .Where(a => a.Public)
            .Select(a => a.Name)
            .ToHashSet() ?? [];
    }

    internal static Player ToModelPlayer(Db.Player player, HashSet<string> publicCustomAttributeNames)
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