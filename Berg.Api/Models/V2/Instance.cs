using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class Instance
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

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