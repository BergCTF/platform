using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Cilium;

public class V2CiliumFQDNRule
{
    [JsonPropertyName("matchName")]
    public string? MatchName { get; set; }

    [JsonPropertyName("matchPattern")]
    public string? MatchPattern { get; set; }
}