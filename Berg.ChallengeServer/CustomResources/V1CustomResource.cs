using System.Text.Json.Serialization;
using k8s;
using k8s.Models;

namespace Berg.ChallengeServer.CustomResources;

public class V1BergCustomResource : KubernetesObject, IMetadata<V1ObjectMeta>
{
    public V1ObjectMeta Metadata { get; set; } = null!;
}

public class V1BergCustomResource<T> : V1BergCustomResource
{
    [JsonPropertyName("spec")]
    public T Spec { get; set; } = default!;
}

public class V1BergCustomResourceList<T> : KubernetesObject
    where T : V1BergCustomResource
{
    public V1ListMeta Metadata { get; set; } = null!;
    public List<T> Items { get; set; } = null!;
}