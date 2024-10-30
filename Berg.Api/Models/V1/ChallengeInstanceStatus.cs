using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class ChallengeInstanceStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("status")]
    public ChallengeInstanceState InstanceState { get; set; } = ChallengeInstanceState.None;

    [JsonPropertyName("services")]
    public List<Service> Services { get; set; } = new();

    [JsonPropertyName("instanceTimeout")]
    public DateTime? InstanceTimeout { get; set; }
}

public enum ChallengeInstanceState
{
    None,
    Starting,
    Running,
    Terminating,
}