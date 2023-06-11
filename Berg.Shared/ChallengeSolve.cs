using System.Text.Json.Serialization;

namespace Berg.Shared;

public class ChallengeSolve
{
    [JsonPropertyName("solvedAt")]
    public DateTime SolvedAt { get; set; }

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = null!;
    
    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }
    
    [JsonPropertyName("teamName")]
    public string? TeamName { get; set; }
    
    [JsonPropertyName("teamId")]
    public Guid? TeamId { get; set; }
}