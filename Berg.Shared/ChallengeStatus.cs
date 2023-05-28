using System.Text.Json.Serialization;

namespace Berg.Shared;

public class ChallengeStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("status")]
    public ChallengeState State { get; set; } = ChallengeState.None;

    [JsonPropertyName("services")]
    public List<Service> Services { get; set; } = new();
}

public enum ChallengeState
{
    None,
    Starting,
    Running,
    Terminating,
}