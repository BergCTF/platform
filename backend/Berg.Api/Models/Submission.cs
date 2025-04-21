using System.Text.Json.Serialization;

namespace Berg.Api.Models;

public class Submission
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }

    [JsonPropertyName("submittedAt")]
    public DateTime SubmittedAt { get; set; }

    [JsonPropertyName("challengeName")]
    public string ChallengeName { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}