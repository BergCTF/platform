using Berg.Api.CustomResources.Berg;
using Berg.Api.Services;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Challenge = Berg.Api.Models.V2.Challenge;
using Attachment = Berg.Api.Models.V2.Attachment;
using Berg.Api.Configuration;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class ChallengeController(IChallengeService challengeService, CtfConfig ctfConfig) : ControllerBase
{
    [HttpGet]
    [Route("/api/v2/challenges")]
    [ProducesResponseType(typeof(List<Challenge>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<List<Challenge>> ListChallenges()
    {
        if (!ctfConfig.AllowAnonymousAccess &&
            !(HttpContext.User.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }
        return challengeService.GetChallenges()
            .Select(ToChallenge)
            .ToList();
    }

    [HttpGet]
    [Route("/api/v2/challenges/{name}")]
    [ProducesResponseType(typeof(Challenge), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Challenge> GetChallenge(string name)
    {
        if (!ctfConfig.AllowAnonymousAccess &&
            !(HttpContext.User.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }
        var challenge = challengeService
            .GetChallenges()
            .FirstOrDefault(c => c.Name() == name);
        if (challenge == null)
            return NotFound();
        return Ok(ToChallenge(challenge));
    }

    private static Challenge ToChallenge(V1Challenge c)
    {
        var challengeName = c.Name();
        return new Challenge
        {
            Name = challengeName,
            Author = c.Spec.Author,
            Description = c.Spec.Description,
            Difficulty = c.Spec.Difficulty,
            FlagFormat = c.Spec.FlagFormat,
            Categories = c.Spec.Categories,
            Tags = c.Spec.Tags,
            Event = c.Spec.Event ?? "",
            HasRemote = c.Spec.Containers?.Any() ?? false,
            Attachments = c.Spec.Attachments?.Select(a => new Attachment
            {
                FileName = a.FileName,
                DownloadUrl = a.DownloadUrl,
            }).ToList() ?? [],
        };
    }
}