using Berg.Api.Models;
using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
using Microsoft.AspNetCore.Mvc;
using k8s;
using Microsoft.AspNetCore.Authorization;

namespace Berg.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName="berg-api")]
public class PageController(Kubernetes kubernetes) : ControllerBase
{
    private readonly GenericClient pageClient = CustomResource.CreateGenericClient<V1Page>(kubernetes, false);

    [HttpGet]
    [Route("/api/pages")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    [ProducesResponseType(typeof(List<Page>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<Page>>> ListPages(CancellationToken cancellationToken)
    {
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