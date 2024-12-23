using Berg.Api.Configuration;
using Berg.Api.Models.V2;
using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
using Microsoft.AspNetCore.Mvc;
using k8s;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class PageController(Kubernetes kubernetes, CtfConfig ctfConfig) : ControllerBase
{
    private readonly GenericClient pageClient = CustomResource.CreateGenericClient<V1Page>(kubernetes, false);

    [HttpGet]
    [Route("/api/v2/pages")]
    [ProducesResponseType(typeof(List<Page>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<Page>>> ListPages(CancellationToken cancellationToken)
    {
        if (!ctfConfig.AllowAnonymousAccess &&
            !(HttpContext.User.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }
        var bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        var pages = await pageClient.ListNamespacedAsync<CustomResourceList<V1Page>>(bergNamespace, cancellationToken);
        return pages.Items
            .Select(ToPage)
            .ToList();
    }

    internal static Page ToPage(V1Page p)
    {
        return new Page{
            Title = p.Spec.Title,
            Path = p.Spec.Path,
            Index = p.Spec.Index,
            Content = p.Spec.Content,
        };
    }
}