using Berg.ChallengeServer.CustomResources;
using k8s;

namespace Berg.ChallengeServer.Cache;

public class ChallengeCache
{
    private readonly GenericClient _challengeClient;
    
    public ChallengeCache(Kubernetes kubernetes)
    {
        _challengeClient = new GenericClient(kubernetes, "berg.norelect.ch", "v1", "challenges", false);
        var ns = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }
}