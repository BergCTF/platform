using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.Configuration;

public class RateLimits
{
    [JsonPropertyName("maxInvalidFlagSubmissionsPerMinute")]
    public int MaxInvalidFlagSubmissionsPerMinute { get; set; } = 5;
    
    [JsonPropertyName("maxInvalidFlagSubmissionsPerHour")]
    public int MaxInvalidFlagSubmissionsPerHour { get; set; } = 10;
    
    [JsonPropertyName("maxInvalidFlagSubmissionsPerDay")]
    public int MaxInvalidFlagSubmissionsPerDay { get; set; } = 25;
}