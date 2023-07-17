using System.Text.Json.Serialization;

namespace Berg.Shared;

public class CtfChallenges
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }
    
    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("teams")]
    public bool Teams { get; set; }

    [JsonPropertyName("challengesByCategory")]
    public Dictionary<string, List<Challenge>> Challenges { get; set; } = new();
}