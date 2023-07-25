using System.Text.Json.Serialization;
using k8s;
using k8s.Models;

namespace Berg.ChallengeServer.CustomResources;

public class CustomResource : KubernetesObject, IMetadata<V1ObjectMeta>
{
    public V1ObjectMeta Metadata { get; set; } = null!;
}

public class CustomResource<T> : CustomResource
{
    [JsonPropertyName("spec")]
    public T Spec { get; set; } = default!;
}

public class CustomResourceList<T> : KubernetesObject
    where T : CustomResource
{
    public V1ListMeta Metadata { get; set; } = null!;
    public List<T> Items { get; set; } = null!;
}