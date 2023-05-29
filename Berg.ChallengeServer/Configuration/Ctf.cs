using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.Configuration;

public class Ctf
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("challengeDomain")]
    public string ChallengeDomain { get; set; }
    
    [JsonPropertyName("teams")]
    public bool Teams { get; set; }
    
    [JsonPropertyName("playerCategories")]
    public List<PlayerCategory>? PlayerCategories { get; set; }

    [JsonPropertyName("scoring")]
    public Scoring Scoring { get; set; } = null!;
    
    [JsonPropertyName("rateLimits")]
    public RateLimits RateLimits { get; set; } = new();
}