using System.Text.Json.Serialization;

namespace Berg.Shared;

public class ChallengeInstanceStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("status")]
    public ChallengeInstanceState InstanceState { get; set; } = ChallengeInstanceState.None;

    [JsonPropertyName("services")]
    public List<Service> Services { get; set; } = new();
}

public enum ChallengeInstanceState
{
    None,
    Starting,
    Running,
    Terminating,
}