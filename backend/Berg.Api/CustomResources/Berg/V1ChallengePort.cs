using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1ChallengePort
{
    [JsonPropertyName("name")]
    public string? Name { get; set; } = null;

    [JsonPropertyName("port")]
    public int Port { get; set; } = 80;

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "tcp";

    [JsonPropertyName("appProtocol")]
    public string AppProtocol { get; set; } = null!;

    [JsonPropertyName("type")]
    public V1ChallengePortType Type { get; set; } = V1ChallengePortType.InternalPort;
}