using System.Text.Json.Serialization;
using k8s.Models;

namespace Berg.ChallengeServer.CustomResources;

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

public class V2CiliumPortRule
{
    [JsonPropertyName("ports")]
    public List<V2CiliumPortProtocol>? Ports { get; set; }
    
    [JsonPropertyName("rules")]
    public V2CiliumL7Rule? Rules { get; set; }
}

public class V2CiliumPortProtocol
{
    [JsonPropertyName("port")]
    public string? Port { get; set; }
    
    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }
}

public class V2CiliumL7Rule
{
    public List<V2CiliumPortRuleDns>? Dns { get; set; }
}

public class V2CiliumPortRuleDns
{
    [JsonPropertyName("matchName")]
    public string? MatchName { get; set; }
    
    [JsonPropertyName("matchPattern")]
    public string? MatchPattern { get; set; }
}

public class V2CiliumFQDNRule
{
    [JsonPropertyName("matchName")]
    public string? MatchName { get; set; }
    
    [JsonPropertyName("matchPattern")]
    public string? MatchPattern { get; set; }
}

public static class CiliumEntity
{
    public const string Host = "host";
    public const string RemoteNode = "remote-node";
    public const string KubeApiServer = "kube-apiserver";
    public const string Ingress = "ingress";
    public const string Cluster = "cluster";
    public const string Init = "init";
    public const string Health = "health";
    public const string Unmanaged = "unmanaged";
    public const string World = "world";
    public const string All = "all";
}

public static class CiliumProtocol
{
    public const string Tcp = "TCP";
    public const string Udp = "UDP";
}