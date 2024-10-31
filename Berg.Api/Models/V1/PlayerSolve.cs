using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class PlayerSolve
{
    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }

    [JsonPropertyName("solvedAt")]
    public DateTime SolvedAt { get; set; }

    [JsonPropertyName("challengeName")]
    public string ChallengeName { get; set; } = "";

    [JsonPropertyName("isFirstBlood")]
    public bool IsFirstBlood { get; set; }
}