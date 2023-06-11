using System.Text.Json.Serialization;

namespace Berg.Shared;

public class PlayerSolve
{
    [JsonPropertyName("solvedAt")]
    public DateTime SolvedAt { get; set; }

    [JsonPropertyName("challengeName")]
    public string ChallengeName { get; set; } = null!;
}