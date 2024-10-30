using System.Security.Claims;
using System.Text.Json.Serialization;
using Berg.Api.Services;
using Berg.Api.Models.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Berg.Api.Models.V2;

namespace Berg.Api.Controllers.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
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
        var instance = await _challengeService.StartChallengeInstance(playerId, challenge, cancel);
        return ToChallengeInstanceStatus(instance);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/challengeInstance/stop")]
    public async Task<ChallengeInstanceStatus> StopChallengeInstance(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var instance = await _challengeService.StopChallengeInstance(playerId, cancel);
        return ToChallengeInstanceStatus(instance);
    }

    internal static ChallengeInstanceStatus ToChallengeInstanceStatus(Instance instance)
    {
        return new ChallengeInstanceStatus
        {
            InstanceState = instance.InstanceState switch
            {
                InstanceState.Starting => ChallengeInstanceState.Starting,
                InstanceState.Running => ChallengeInstanceState.Running,
                InstanceState.Terminating => ChallengeInstanceState.Terminating,
                InstanceState.None => ChallengeInstanceState.None,
                _ => ChallengeInstanceState.None
            },
            InstanceTimeout = instance.Timeout,
            Name = instance.Name,
            Services = instance.Services.Select(s => new Models.V1.Service
            {
                AppProtocol = s.AppProtocol,
                Hostname = s.Hostname,
                Name = s.Name,
                Port = s.Port,
                Protocol = s.Protocol,
                VHost = s.VHost
            }).ToList()
        };
    }
}