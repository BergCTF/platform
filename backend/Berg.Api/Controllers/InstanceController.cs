using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Models;
using Berg.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Instance = Berg.Api.Models.Instance;

namespace Berg.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "berg-api")]
public class InstanceController(
    IChallengeService challengeService,
    IChallengeInstanceService challengeInstanceService,
    BergDbContext bergDbContext,
    CtfConfig ctfConfig) : ControllerBase
{
    public class InstanceStartRequest
    {
        [JsonPropertyName("challenge")]
        public string? Challenge { get; set; }
    }

    [HttpGet]
    [Route("/api/instances")]
    [Authorize(Policy = Constants.Policies.Admin)]
    [ProducesResponseType(typeof(List<Instance>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Instance>>> GetAllChallengeInstances(CancellationToken cancel)
    {
        var instances = await challengeInstanceService.GetChallengeInstances(cancel);
        return instances.Select(cr => Instance.FromCR(cr)).ToList();
    }

    [HttpGet]
    [Route("/api/instances/historic")]
    [Authorize(Policy = Constants.Policies.Admin)]
    [ProducesResponseType(typeof(List<Instance>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Instance>>> GetAllHistoricChallengeInstances(CancellationToken cancel)
    {
        return await bergDbContext.Instances.Where(i => i.TerminatedAt.HasValue).Select(i => new Instance
        {
            Id = i.Id,
            ChallengeName = i.ChallengeName,
            PlayerId = i.PlayerId,
            InstanceState = InstanceState.None,
            StartedAt = i.StartedAt,
            TerminatedAt = i.TerminatedAt,
        }).ToListAsync(cancel);
    }

    [HttpGet]
    [Route("/api/instances/current")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(Instance), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Instance>> GetChallengeInstance(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var ci = await challengeInstanceService.GetChallengeInstance(playerId, cancel);
        if (ci == null)
        {
            return NotFound();
        }
        return Instance.FromCR(ci);
    }

    [HttpPost]
    [Route("/api/instances/current")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(Instance), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Instance>> StartChallengeInstance([FromBody] InstanceStartRequest startRequest,
        CancellationToken cancel)
    {
        if (string.IsNullOrEmpty(startRequest.Challenge))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad request",
                Detail = "Challenge can't be null"
            });
        }

        var utcNow = DateTime.UtcNow;
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var isAdmin = User.HasClaim(OpenIddictConstants.Claims.Role, Constants.Roles.Admin);

        if (utcNow < ctfConfig.Start && !isAdmin)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Instances can only be started after the start of the ctf.",
            });
        }
        var challenge = await challengeService.GetChallenge(startRequest.Challenge, cancel);
        if (challenge == null || (challenge.Spec.HideUntil != null && utcNow < challenge.Spec.HideUntil && !isAdmin))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Challenge not found",
            });
        }

        var ci = await challengeInstanceService.CreateChallengeInstance(playerId, challenge, cancel);
        return Instance.FromCR(ci);
    }

    [HttpDelete]
    [Route("/api/instances/current")]
    [Authorize(Policy = Constants.Policies.Player)]
    [ProducesResponseType(typeof(Instance), StatusCodes.Status200OK)]
    public async Task<ActionResult<Instance>> StopChallengeInstance(CancellationToken cancel)
    {
        var playerId = Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var ci = await challengeInstanceService.DeleteChallengeInstance(playerId, cancel);
        var instance = Instance.FromCR(ci);
        instance.InstanceState = InstanceState.Terminating;
        return instance;
    }
}
