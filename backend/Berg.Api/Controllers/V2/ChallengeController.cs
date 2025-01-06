using Berg.Api.CustomResources.Berg;
using Berg.Api.Services;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Challenge = Berg.Api.Models.V2.Challenge;
using Attachment = Berg.Api.Models.V2.Attachment;
using Berg.Api.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class ChallengeController(
    IChallengeService challengeService,
    HttpClient httpClient,
    InfraConfig infraConfig,
    CtfConfig ctfConfig) : ControllerBase
{
    [HttpGet]
    [Route("/api/v2/challenges")]
    [Authorize(Policy = Constants.Policies.Anonymous)]
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
    [Authorize(Policy = Constants.Policies.Anonymous)]
    [ProducesResponseType(typeof(Challenge), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Challenge> GetChallenge([FromRoute] string name)
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

    [HttpGet]
    [Route("/api/v2/challenges/{name}/handout/{index}")]
    [Authorize(Policy = Constants.Policies.Anonymous)]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChallengeHandout([FromRoute] string name, [FromRoute] int index, CancellationToken cancellationToken)
    {
        if (!ctfConfig.AllowAnonymousAccess &&
            !(HttpContext.User.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }

        var utcNow = DateTime.UtcNow;
        if (utcNow < ctfConfig.Start)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Handouts can only be downloaded after the start of the ctf.",
            });
        }

        if (infraConfig.HandoutServiceUrl == null) {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "This instance is not configured to host handouts.",
            });
        }

        var challenge = challengeService
            .GetChallenges()
            .FirstOrDefault(c => c.Name() == name);
        if (challenge == null)
            return NotFound();
        if (utcNow < challenge.Spec.HideUntil)
            return NotFound();
        var attachment = challenge.Spec.Attachments?.ElementAtOrDefault(index);
        if (attachment == null)
            return NotFound();

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentType = "application/octet-stream";
        Response.Headers.ContentDisposition = $"attachment; filename={attachment.FileName}";

        var uri = new UriBuilder(new Uri(infraConfig.HandoutServiceUrl)) {
            Path = attachment.DownloadUrl,
        }.Uri;
        var stream = await httpClient.GetStreamAsync(uri, cancellationToken);
        return new FileStreamResult(stream, "application/octet-stream");
    }

    internal static Challenge ToChallenge(V1Challenge c)
    {
        var challengeName = c.Name();
        return new Challenge
        {
            Name = challengeName,
            DisplayName = c.Spec.DisplayName ?? challengeName,
            Author = c.Spec.Author,
            Description = c.Spec.Description,
            Difficulty = c.Spec.Difficulty,
            FlagFormat = c.Spec.FlagFormat,
            Categories = c.Spec.Categories,
            Tags = c.Spec.Tags,
            Event = c.Spec.Event ?? "",
            HasRemote = c.Spec.Containers?.Any() ?? false,
            Attachments = c.Spec.Attachments?.Select((a, i) => {
                var url = new Uri(a.DownloadUrl);
                // Only rewrite relative urls, since other urls can point to external file
                // hosting services. We do not want to proxy those attachments as those links
                // are not guessable and might require user interaction with the website to download.
                // Examples of this are Microsoft OneDrive or Google Drive Links.
                if (!string.IsNullOrEmpty(url.Host)) {
                    return new Attachment
                    {
                        FileName = a.FileName,
                        DownloadUrl = a.DownloadUrl,
                    };
                } else {
                    return new Attachment
                    {
                        FileName = a.FileName,
                        DownloadUrl = $"/api/v2/challenges/{challengeName}/handout/{i}",
                    };
                }
            }).ToList() ?? [],
        };
    }
}