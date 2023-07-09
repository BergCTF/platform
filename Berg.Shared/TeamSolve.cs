using System.Text.Json.Serialization;

namespace Berg.Shared;

public class TeamSolve
{
    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }
    
    [JsonPropertyName("solvedAt")]
    public DateTime SolvedAt { get; set; }

    [JsonPropertyName("challengeName")]
    public string ChallengeName { get; set; } = null!;
}