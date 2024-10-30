using System.Security.Claims;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Services;
using Berg.Api.Models.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Player = Berg.Api.Models.V1.Player;

namespace Berg.Api.Controllers.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
public class PlayerController(
    IChallengeService challengeService,
    CtfConfig ctfConfig,
    BergDbContext dbContext,
    V2.PlayerController v2PlayerController) : ControllerBase
{

    [HttpGet]
    [Route("/api/v1/players")]
    public async Task<List<Player>> ListPlayers(CancellationToken cancel)
    {
        var publicCustomAttributes = ctfConfig.PlayerAttributes?
            .Where(a => a.Public).Select(a => a.Name)
            .ToHashSet() ?? [];
        var players = await dbContext.Players
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
            RequiredAttributes = [],
        }).ToList();
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("/api/v1/self")]
    public async Task<PlayerSelf> GetPlayerSelf(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = dbContext.Players
            .Include(p => p.Attributes)
            .Single(p => p.Id == playerId);
        var requiredAttributes = ctfConfig.PlayerAttributes?
            .Where(a => a.Required).ToHashSet() ?? [];
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
            ChallengeInstance = InstanceController.ToChallengeInstanceStatus(await challengeService.GetChallengeInstance(player.Id, cancel))
        };
    }

    public class PlayerUpdateRequest
    {
        public Dictionary<string, string> Attributes { get; set; } = [];
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/self")]
    public void UpdatePlayerSelf(PlayerUpdateRequest playerUpdate)
    {
        v2PlayerController.UpdatePlayerAttributes(new V2.PlayerController.AttributesUpdateRequest{
            Attributes = playerUpdate.Attributes,
        });
    }

    [HttpDelete]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/self/api-key")]
    public ActionResult<string> DeleteApiKey()
    {
        return v2PlayerController.ResetApiKey();
    }

    [HttpDelete]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/self")]
    public IActionResult DeleteSelf()
    {
        return v2PlayerController.DeleteMe();
    }
}
