using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class Instance
{
    [JsonPropertyName("id")]
    public required Guid? Id { get; set; }

    [JsonPropertyName("playerId")]
    public required Guid? PlayerId { get; set; }

    [JsonPropertyName("name")]
    public required string ChallengeName { get; set; } = "";

    [JsonPropertyName("status")]
    public InstanceState InstanceState { get; set; } = InstanceState.None;

    [JsonPropertyName("services")]
    public List<Service> Services { get; set; } = [];

    [JsonPropertyName("timeout")]
    public DateTime? Timeout { get; set; }
}

public enum InstanceState
{
    None,
    Starting,
    Running,
    Terminating,
}

public class Service
{
    [JsonPropertyName("name")]
    public string? Name { get; set; } = null;

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "";

    [JsonPropertyName("appProtocol")]
    public string AppProtocol { get; set; } = "";

    [JsonPropertyName("tls")]
    public bool Tls { get; set; } = false;
}