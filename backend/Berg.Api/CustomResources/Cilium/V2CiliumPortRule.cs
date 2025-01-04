using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Cilium;

public class V2CiliumPortRule
{
    [JsonPropertyName("ports")]
    public List<V2CiliumPortProtocol>? Ports { get; set; }

    [JsonPropertyName("rules")]
    public V2CiliumL7Rule? Rules { get; set; }
}