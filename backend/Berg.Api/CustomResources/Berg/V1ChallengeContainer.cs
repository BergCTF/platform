using System.Text.Json.Serialization;
using k8s.Models;

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

    [JsonPropertyName("resourceRequests")]
    public Dictionary<string, string>? ResourceRequests { get; set; }

    [JsonPropertyName("readinessProbe")]
    public V1Probe? ReadinessProbe { get; set; }

    [JsonPropertyName("runtimeClassName")]
    public string? RuntimeClassName { get; set; }

    [JsonPropertyName("egressBandwidth")]
    public string? EgressBandwidth { get; set; }

    [JsonPropertyName("ingressBandwidth")]
    public string? IngressBandwidth { get; set; }

    [JsonPropertyName("ports")]
    public List<V1ChallengePort>? Ports { get; set; }

    [JsonPropertyName("additionalCapabilities")]
    public List<string>? AdditionalCapabilities { get; set; }
}