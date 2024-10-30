using Berg.Api.CustomResources.Berg;
using Berg.Api.Services;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Challenge = Berg.Api.Models.V2.Challenge;
using Attachment = Berg.Api.Models.V2.Attachment;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class ChallengeController(IChallengeService challengeService) : ControllerBase
{
    [HttpGet]
    [Route("/api/v2/challenges")]
    public List<Challenge> ListChallenges()
    {
        return challengeService.GetChallenges()
                .Select(ToChallenge)
                .ToList();
    }

    [HttpGet]
    [Route("/api/v2/challenges/{name}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Challenge> GetChallenge(string name)
    {
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
            Instantiable = c.Spec.Containers?.Any() ?? false,
            Attachments = c.Spec.Attachments?.Select(a => new Attachment
            {
                FileName = a.FileName,
                DownloadUrl = a.DownloadUrl,
            }).ToList() ?? [],
        };
    }
}