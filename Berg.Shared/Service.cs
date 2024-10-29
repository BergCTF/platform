using System.Text.Json.Serialization;

namespace Berg.Shared;

public class Service
{
    [JsonPropertyName("name")]
    public string? Name { get; set; } = null;

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = null!;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = null!;

    [JsonPropertyName("appProtocol")]
    public string AppProtocol { get; set; } = null!;

    [JsonPropertyName("vhost")]
    public bool VHost { get; set; } = false;
}