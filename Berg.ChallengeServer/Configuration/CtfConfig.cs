using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.Configuration;

public class CtfConfig
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("challengeDomain")]
    public string ChallengeDomain { get; set; } = "localhost";

    [JsonPropertyName("teams")]
    public bool Teams { get; set; } = false;

    [JsonPropertyName("playerCategories")]
    public List<PlayerCategory>? PlayerCategories { get; set; } = new();

    [JsonPropertyName("scoring")]
    public Scoring Scoring { get; set; } = new();
    
    [JsonPropertyName("rateLimits")]
    public RateLimits RateLimits { get; set; } = new();
    
    [JsonPropertyName("configDbSyncInterval")]
    public TimeSpan ConfigDbSyncInterval { get; set; } = TimeSpan.FromSeconds(15);
}