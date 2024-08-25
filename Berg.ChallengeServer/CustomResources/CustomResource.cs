using System.Text.Json.Serialization;
using k8s;
using k8s.Models;

namespace Berg.ChallengeServer.CustomResources;

public abstract class CustomResource : KubernetesObject, IMetadata<V1ObjectMeta>
{
    protected CustomResource(string kind, string plural, string group, string version)
    {
        Kind = kind;
        Plural = plural;
        Group = group;
        Version = version;
        ApiVersion = $"{group}/{version}";
    }
    
    public V1ObjectMeta Metadata { get; set; } = null!;

    [JsonIgnore]
    public string Plural { get; set; }
    
    [JsonIgnore]
    public string Group { get; set; }
    
    [JsonIgnore]
    public string Version { get; set; }

    public static GenericClient CreateGenericClient<T>(IKubernetes kubernetes, bool disposeClient = true)
        where T : CustomResource, new()
    {
        var customResource = new T();
        return new GenericClient(kubernetes, customResource.Group, customResource.Version, customResource.Plural, disposeClient);
    }
}

public abstract class CustomResource<T> : CustomResource
{
    protected CustomResource(string kind, string plural, string group, string version) : base(kind, plural, group, version)
    {
    }
    
    [JsonPropertyName("spec")]
    public T Spec { get; set; } = default!;
}