using System.Text.Json.Serialization;

namespace Berg.Shared;

public class ActivityEntry
{
    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }
    
    [JsonPropertyName("solvedAt")]
    public DateTime SolvedAt { get; set; }

    [JsonPropertyName("teamId")]
    public Guid? TeamId { get; set; }
    
    [JsonPropertyName("challengeName")]
    public string ChallengeName { get; set; } = null!;
}