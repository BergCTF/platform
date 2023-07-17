using System.Text.Json.Serialization;

namespace Berg.Shared;

public class PlayerSelf
{
    [JsonPropertyName("player")]
    public Player? Player { get; set; }
    
    [JsonPropertyName("challengeInstance")]
    public ChallengeInstanceStatus ChallengeInstance { get; set; } = new();
}