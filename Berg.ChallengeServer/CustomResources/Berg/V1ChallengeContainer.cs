using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources.Berg;

public class V1ChallengeContainer
{
    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = null!;
    
    [JsonPropertyName("image")]
    public string Image { get; set; } = null!;
    
    [JsonPropertyName("environment")]
    public Dictionary<string, object>? Environment { get; set; }
    
    [JsonPropertyName("resourceLimits")]
    public Dictionary<string, string>? ResourceLimits { get; set; }
    
    [JsonPropertyName("ports")]
    public List<V1ChallengePort>? Ports { get; set; }
}