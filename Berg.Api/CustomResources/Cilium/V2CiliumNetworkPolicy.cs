using System.Text.Json.Serialization;
using k8s.Models;

namespace Berg.Api.CustomResources.Cilium;

public class V2CiliumNetworkPolicy : CustomResource<V2CiliumNetworkPolicySpec>
{
    public V2CiliumNetworkPolicy() : base(
        "CiliumNetworkPolicy",
        "ciliumnetworkpolicies",
        "cilium.io",
        "v2")
    {
    }

    [JsonPropertyName("specs")]
    public List<V2CiliumNetworkPolicySpec>? Specs { get; set; }
}

public class V2CiliumNetworkPolicySpec
{
    [JsonPropertyName("endpointSelector")]
    public V1LabelSelector? EndpointSelector { get; set; }

    [JsonPropertyName("egress")]
    public List<V2CiliumEgressRule>? Egress { get; set; }
}