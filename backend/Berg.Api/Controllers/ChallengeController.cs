using Attachment = Berg.Api.Models.Attachment;
using Berg.Api.Configuration;
using Berg.Api.CustomResources.Berg;
using Berg.Api.Resources;
using Berg.Api.Services;
using Challenge = Berg.Api.Models.Challenge;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Berg.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "berg-api")]
public class ChallengeController(
    IChallengeService challengeService,
    Kubernetes kubernetes,
    KubernetesClientConfiguration kubernetesConfig,
    HttpClient httpClient,
    ILogger<ChallengeController> logger,
    InfraConfig infraConfig,
    CtfConfig ctfConfig) : ControllerBase
{
    [HttpGet]
    [Route("/api/challenges")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [ProducesResponseType(typeof(List<Challenge>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<Challenge>>> ListChallenges(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var isAdmin = User.HasClaim(OpenIddictConstants.Claims.Role, Constants.Roles.Admin);

        if (utcNow < ctfConfig.Start && !isAdmin)
        {
            return Ok(new List<Challenge>());
        }

        var challenges = await challengeService.GetChallenges(cancellationToken);
        return challenges
            .Where(c => c.Spec.HideUntil == null || isAdmin || c.Spec.HideUntil <= utcNow)
            .Select(ToChallenge)
            .ToList();
    }

    [HttpGet]
    [Route("/api/challenges/{name}")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [ProducesResponseType(typeof(Challenge), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Challenge>> GetChallenge([FromRoute] string name, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var isAdmin = User.HasClaim(OpenIddictConstants.Claims.Role, Constants.Roles.Admin);

        if (utcNow < ctfConfig.Start && !isAdmin)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "The ctf has not started yet.",
            });
        }

        var challenge = await challengeService.GetChallenge(name, cancellationToken);
        if (challenge == null)
            return NotFound();
        if (challenge.Spec.HideUntil != null && !isAdmin && utcNow < challenge.Spec.HideUntil)
            return NotFound();
        return Ok(ToChallenge(challenge));
    }

    [HttpGet]
    [Route("/api/challenges/{name}/handout/{index}")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChallengeHandout([FromRoute] string name, [FromRoute] int index, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var isAdmin = User.HasClaim(OpenIddictConstants.Claims.Role, Constants.Roles.Admin);
        if (utcNow < ctfConfig.Start && !isAdmin)
        {
            logger.LogWarning("Non admin user tried to access handout before ctf start");
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Handouts can only be downloaded after the start of the ctf.",
            });
        }

        var challenge = await challengeService.GetChallenge(name, cancellationToken);
        if (challenge == null)
            return NotFound();
        var hideUntil = challenge.Spec.HideUntil;
        if (hideUntil.HasValue && !isAdmin && utcNow < hideUntil)
            return NotFound();
        var attachment = challenge.Spec.Attachments?.ElementAtOrDefault(index);
        if (attachment == null)
            return NotFound();

        if (!string.IsNullOrEmpty(attachment.DownloadUrl))
        {
            if (infraConfig.HandoutServiceUrl == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Bad Request",
                    Detail = "This instance is not configured to host handouts.",
                });
            }

            // Download from an internal webserver that requires no authentication
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.ContentType = "application/octet-stream";
            Response.Headers.ContentDisposition = $"attachment; filename={attachment.FileName}";
            var uri = new UriBuilder(new Uri(infraConfig.HandoutServiceUrl))
            {
                Path = attachment.DownloadUrl,
            }.Uri;
            var stream = await httpClient.GetStreamAsync(uri, cancellationToken);
            return new FileStreamResult(stream, "application/octet-stream");
        }

        if (!string.IsNullOrEmpty(attachment.DownloadImage))
        {
            // Download from a docker image registry
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.ContentType = "application/octet-stream";
            Response.Headers.ContentDisposition = $"attachment; filename={attachment.FileName}";

            var reference = Reference.Parse(attachment.DownloadImage);

            var pullSecretName = attachment.DownloadImagePullSecret ?? infraConfig.PullSecretName;
            DockerConfig? dockerConfig = null;
            if (!string.IsNullOrWhiteSpace(pullSecretName))
            {
                var pullSecret = await kubernetes.ReadNamespacedSecretAsync(pullSecretName, kubernetesConfig.Namespace, cancellationToken: cancellationToken);
                if (pullSecret.Type != "kubernetes.io/dockerconfigjson")
                {
                    return Problem(
                        title: "Invalid attachment pull secret",
                        detail: $"The pull secret specified for this attachment has the wrong type: {pullSecret.Type}"
                    );
                }
                var dockerConfigJson = pullSecret.Data[".dockerconfigjson"];
                dockerConfig = JsonSerializer.Deserialize<DockerConfig>(dockerConfigJson);
            }

            ICredentialProvider? credentialProvider = null;
            if (dockerConfig?.Authentications?.TryGetValue(reference.Host, out DockerAuth? auth) ?? false)
            {
                if (auth == null || string.IsNullOrEmpty(auth.Authentication))
                    return Problem(title: "Invalid handout pull secret", detail: "The pull secret specified for this attachment has no creds.");
                logger.LogDebug("Using ORAS client with basic authentication");
                var usernamePasswordPair = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Authentication));
                var splitAt = usernamePasswordPair.IndexOf(':');
                var username = usernamePasswordPair[0..splitAt];
                var password = usernamePasswordPair[(splitAt + 1)..];
                credentialProvider = new SingleRegistryCredentialProvider(reference.Registry, new Credential(username, password));
            }
            var client = new Client(credentialProvider: credentialProvider);

            var registry = new Registry(new RepositoryOptions
            {
                Client = client,
                Reference = new Reference(reference.Registry),
                PlainHttp = attachment.DownloadImageInsecure,
            });
            try
            {
                var repo = await registry.GetRepositoryAsync(reference.Repository, cancellationToken);
                var (_, manifestStream) = await repo.Manifests.FetchAsync(reference.ContentReference, cancellationToken);
                var manifest = JsonSerializer.Deserialize<Manifest>(manifestStream);
                var layer = manifest.Layers.Single();
                Response.ContentLength = layer.Size;
                var stream = await repo.Blobs.FetchAsync(layer, cancellationToken);
                return new FileStreamResult(stream, "application/octet-stream");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while trying to fetch handout from OCI registry");
                return Problem(title: "Server error", detail: "Error fetching handout from upstream.");
            }
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Bad Request",
            Detail = "This handout is not properly configured.",
        });
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
            HideUntil = c.Spec.HideUntil,
            FlagFormat = c.Spec.FlagFormat,
            Categories = c.Spec.Categories,
            Tags = c.Spec.Tags,
            Event = c.Spec.Event ?? "",
            HasRemote = c.Spec.Containers?.Any() ?? false,
            Attachments = c.Spec.Attachments?.Select((a, i) =>
            {
                if (!string.IsNullOrEmpty(a.DownloadImage))
                {
                    return new Attachment
                    {
                        FileName = a.FileName,
                        DownloadUrl = $"/api/challenges/{challengeName}/handout/{i}",
                    };
                }

                var url = new Uri(a.DownloadUrl);
                // Only rewrite relative urls, since other urls can point to external file
                // hosting services. We do not want to proxy those attachments as those links
                // are not guessable and might require user interaction with the website to download.
                // Examples of this are Microsoft OneDrive or Google Drive Links.
                if (!string.IsNullOrEmpty(url.Host))
                {
                    return new Attachment
                    {
                        FileName = a.FileName,
                        DownloadUrl = a.DownloadUrl,
                    };
                }
                else
                {
                    return new Attachment
                    {
                        FileName = a.FileName,
                        DownloadUrl = $"/api/challenges/{challengeName}/handout/{i}",
                    };
                }
            }).ToList() ?? [],
        };
    }
}