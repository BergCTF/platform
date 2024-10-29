using System.Text.Json.Serialization;

namespace Berg.Shared;

public class PlayerSolve
{
    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }

    [JsonPropertyName("solvedAt")]
    public DateTime SolvedAt { get; set; }

    [JsonPropertyName("challengeName")]
    public string ChallengeName { get; set; } = null!;

    [JsonPropertyName("isFirstBlood")]
    public bool IsFirstBlood { get; set; }
}