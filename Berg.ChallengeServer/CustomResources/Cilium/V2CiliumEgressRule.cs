using System.Text.Json.Serialization;
using k8s.Models;

namespace Berg.ChallengeServer.CustomResources.Cilium;

public class V2CiliumEgressRule
{
    [JsonPropertyName("toEndpoints")]
    public List<V1LabelSelector>? ToEndpoints { get; set; }

    [JsonPropertyName("toEntities")]
    public List<string>? ToEntities { get; set; }

    [JsonPropertyName("toFQDNs")]
    public List<V2CiliumFQDNRule>? ToFQDNs { get; set; }

    [JsonPropertyName("toPorts")]
    public List<V2CiliumPortRule>? ToPorts { get; set; }
}