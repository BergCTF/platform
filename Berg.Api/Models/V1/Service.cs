using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class Service
{
    [JsonPropertyName("name")]
    public string? Name { get; set; } = "";

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "";

    [JsonPropertyName("appProtocol")]
    public string AppProtocol { get; set; } = "";

    [JsonPropertyName("vhost")]
    public bool VHost { get; set; } = false;
}