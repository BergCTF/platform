using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1ChallengeContainer
{
    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = null!;

    [JsonPropertyName("image")]
    public string Image { get; set; } = null!;

    [JsonPropertyName("dynamicFlag")]
    public V1DynamicFlag? DynamicFlag { get; set; } = null;

    [JsonPropertyName("environment")]
    public Dictionary<string, object>? Environment { get; set; }

    [JsonPropertyName("resourceLimits")]
    public Dictionary<string, string>? ResourceLimits { get; set; }

    [JsonPropertyName("runtimeClassName")]
    public string? RuntimeClassName { get; set; }

    [JsonPropertyName("egressBandwidth")]
    public string? EgressBandwidth { get; set; }

    [JsonPropertyName("ports")]
    public List<V1ChallengePort>? Ports { get; set; }

    [JsonPropertyName("additionalCapabilities")]
    public List<string>? AdditionalCapabilities { get; set; }
}