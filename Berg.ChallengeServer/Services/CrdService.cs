using Berg.ChallengeServer.CustomResources;
using k8s;

namespace Berg.ChallengeServer.Services;

public class CrdService
{
    private const string BergGroup    = "berg.norelect.ch";
    private const string TraefikGroup = "traefik.containo.us";
    
    private readonly GenericClient _challengeClient;
    private readonly GenericClient _ingressRouteTcpClient;
    private readonly string _namespace;
    
    public CrdService(Kubernetes kubernetes)
    {
        _challengeClient = new GenericClient(kubernetes, BergGroup, "v1", "challenges", false);
        _ingressRouteTcpClient = new GenericClient(kubernetes, TraefikGroup, "v1alpha1", "ingressroutetcps", false);
        _namespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }

    public async Task<V1TraefikIngressRouteTcp> CreateIngressRouteTcpAsync(
        V1TraefikIngressRouteTcp traefikIngressRouteTcp, string ns, CancellationToken cancellationToken)
    {
        return await _ingressRouteTcpClient.CreateNamespacedAsync(traefikIngressRouteTcp, ns, cancellationToken);
    }

}