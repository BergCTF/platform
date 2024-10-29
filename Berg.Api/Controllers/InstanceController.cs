using System.Security.Claims;
using System.Text.Json.Serialization;
using Berg.Api.Services;
using Berg.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Berg.Api.Controllers;

[ApiController]
public class InstanceController : ControllerBase
{
    private readonly IChallengeService _challengeService;

    public InstanceController(
        IChallengeService challengeService)
    {
        _challengeService = challengeService;
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
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        return await _challengeService.StartChallengeInstance(playerId, challenge, cancel);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/challengeInstance/stop")]
    public async Task<ChallengeInstanceStatus> StopChallengeInstance(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        return await _challengeService.StopChallengeInstance(playerId, cancel);
    }
}