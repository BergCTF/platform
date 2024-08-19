using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources.Cilium;

public class V2CiliumPortProtocol
{
    [JsonPropertyName("port")]
    public string? Port { get; set; }
    
    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }
}