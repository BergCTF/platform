using Berg.ChallengeServer.CustomResources;
using Berg.Shared;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
[Route("/api/v1/ctf")]
public class CtfController : Controller
{
    private readonly ILogger<CtfController> _logger;
    private readonly GenericClient _ctfClient;
    private readonly string _namespace;

    public CtfController(
        ILogger<CtfController> logger,
        IKubernetes kubernetes
    ) {
        _logger = logger;
        _ctfClient = new GenericClient(kubernetes, "berg.norelect.ch", "v1", "ctfs", false);
        _namespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        _logger.LogInformation("CtfController for namespace: {}", _namespace);
    }
    
    [HttpGet]
    public async Task<IEnumerable<Ctf>?> Get(CancellationToken cancel)
    {
        var ctfList = await _ctfClient.ListNamespacedAsync<V1BergCustomResourceList<V1Ctf>>(_namespace, cancel);
        return ctfList.Items.Select(ToCtf);
    }

    [HttpPost]
    public async Task<Ctf> Create(Ctf newCtf, CancellationToken cancel)
    {
        var ctf = FromCtf(newCtf);
        var returnedCtf = await _ctfClient.CreateNamespacedAsync(ctf, _namespace, cancel);
        _logger.LogInformation("Created ctf {}", newCtf.Name);
        return ToCtf(returnedCtf);
    }
    
    [HttpPatch]
    public async Task<Ctf> Modify(Ctf updatedCtf, CancellationToken cancel)
    {
        var ctf = FromCtf(updatedCtf);
        var returnedCtf = await _ctfClient.ReplaceNamespacedAsync(ctf, _namespace, updatedCtf.Name, cancel);
        _logger.LogInformation("Updated ctf {}", updatedCtf.Name);
        return ToCtf(returnedCtf);
    }

    [HttpDelete]
    public async Task<Ctf> Delete(string name, CancellationToken cancel)
    {
        var ctf = await _ctfClient.DeleteNamespacedAsync<V1Ctf>(_namespace, name, cancel);
        _logger.LogInformation("Deleted ctf {}", name);
        return ToCtf(ctf);
    }

    private static V1Ctf FromCtf(Ctf c)
    {
        return new V1Ctf
        {
            Metadata = new V1ObjectMeta
            {
                Name = c.Name,
            },
            Spec = new V1CtfSpec
            {
                Start = c.Start,
                End = c.End,
                Scoring = new V1CtfScoring
                {
                    RateLimits = new V1CtfRateLimits()
                }
            }
        };
    }
    
    private static Ctf ToCtf(V1Ctf c)
    {
        var utcNow = DateTime.UtcNow;
        return new Ctf
        {
            Name = c.Name(),
            Start = c.Spec.Start,
            End = c.Spec.End,
            IsActive = c.Spec.Start <= utcNow && utcNow < c.Spec.End,
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _ctfClient.Dispose();
    }
}