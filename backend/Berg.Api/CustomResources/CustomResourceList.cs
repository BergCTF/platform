using k8s;
using k8s.Models;

namespace Berg.Api.CustomResources;

public class CustomResourceList<T> : KubernetesObject
    where T : CustomResource
{
    public V1ListMeta Metadata { get; set; } = null!;
    public List<T> Items { get; set; } = null!;
}