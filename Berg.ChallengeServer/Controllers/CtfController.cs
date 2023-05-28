using Berg.ChallengeServer.CustomResources;
using Berg.Shared;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
[Route("/api/v1/ctfs")]
public class CtfController : Controller
{
    private readonly GenericClient _ctfClient;
    private readonly string _namespace;

    public CtfController(Kubernetes kubernetes) {
        _ctfClient = new GenericClient(kubernetes, "berg.norelect.ch", "v1", "ctfs", false);
        _namespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }

    [HttpGet]
    public async Task<IEnumerable<Ctf>?> GetCtfs(CancellationToken cancel)
    {
        var ctfList = await _ctfClient.ListNamespacedAsync<V1BergCustomResourceList<V1Ctf>>(_namespace, cancel);
        var utcNow = DateTime.UtcNow;
        return ctfList.Items.Select(c => new Ctf
        {
            Name = c.Name(),
            Start = c.Spec.Start,
            End = c.Spec.End,
            IsActive = c.Spec.Start <= utcNow && utcNow < c.Spec.End,
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _ctfClient.Dispose();
    }
}