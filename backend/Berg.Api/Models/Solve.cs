using System.Text.Json.Serialization;

namespace Berg.Api.Models;

public class Solve
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }

    [JsonPropertyName("solvedAt")]
    public DateTime SolvedAt { get; set; }

    [JsonPropertyName("challengeName")]
    public string ChallengeName { get; set; } = "";
}