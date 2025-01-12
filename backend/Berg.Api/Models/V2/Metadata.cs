using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class Metadata
{
    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("eventName")]
    public required string EventName { get; set; }

    [JsonPropertyName("eventOrganiser")]
    public required string EventOrganiser { get; set; }

    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("allowAnonymousAccess")]
    public bool AllowAnonymousAccess { get; set; }

    [JsonPropertyName("playerAttributes")]
    public List<PlayerAttribute> PlayerAttributes { get; set; } = [];

    [JsonPropertyName("freezeStart")]
    public DateTime? FreezeStart { get; set; }

    [JsonPropertyName("freezeEnd")]
    public DateTime? FreezeEnd { get; set; }

    [JsonPropertyName("teams")]
    public bool Teams { get; set; }

    [JsonPropertyName("challengeMaximumValue")]
    public int ChallengeMaximumValue { get; set; }

    [JsonPropertyName("challengeMinimumValue")]
    public int ChallengeMinimumValue { get; set; }

    [JsonPropertyName("challengeSolvesBeforeMinimum")]
    public int ChallengeSolvesBeforeMinimum { get; set; }
}