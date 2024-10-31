using Berg.Api.Models.V2;
using Berg.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class InstanceController(IChallengeService challengeService) : ControllerBase
{
    private readonly IChallengeService _challengeService = challengeService;

    public class InstanceStartRequest
    {
        [JsonPropertyName("challenge")]
        public string? Challenge { get; set; }
    }

    [HttpGet]
    [Authorize]
    [Route("/api/v2/instances/own")]
    public async Task<ActionResult<Instance>> GetChallengeInstance(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        return await _challengeService.GetChallengeInstance(playerId, cancel);
    }

    [HttpPost]
    [Authorize]
    [Route("/api/v2/instances/own/start")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Instance>> StartChallengeInstance([FromBody] InstanceStartRequest startRequest,
        CancellationToken cancel)
    {
        if (string.IsNullOrEmpty(startRequest.Challenge))
        {
            return BadRequest(new ProblemDetails{
                Title = "Bad request",
                Detail = "Challenge can't be null"
            });
        }
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        return await _challengeService.StartChallengeInstance(playerId, startRequest.Challenge, cancel);
    }

    [HttpPost]
    [Authorize]
    [Route("/api/v2/instances/own/stop")]
    public async Task<ActionResult<Instance>> StopChallengeInstance(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        return await _challengeService.StopChallengeInstance(playerId, cancel);
    }
}