using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources;

public class V1Ctf : V1BergCustomResource<V1CtfSpec>
{
}

public class V1CtfSpec
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("scoring")]
    public V1CtfScoring Scoring { get; set; } = null!;
}

public class V1CtfScoring
{
    [JsonPropertyName("maximumScore")]
    public int MaximumScore { get; set; } = 500;
    
    [JsonPropertyName("minimalScore")]
    public int MinimumScore { get; set; } = 100;
    
    [JsonPropertyName("solvesBeforeMinimum")]
    public int NumSolvesBeforeMinimum { get; set; } = 45;
    
    [JsonPropertyName("freezeStart")]
    public DateTime FreezeStart { get; set; }
    
    [JsonPropertyName("freezeEnd")]
    public DateTime FreezeEnd { get; set; }
    
    [JsonPropertyName("teams")]
    public bool Teams { get; set; }

    [JsonPropertyName("playerCategories")]
    public List<V1CtfPlayerCategory>? PlayerCategories { get; set; }

    [JsonPropertyName("rateLimits")]
    public V1CtfRateLimits RateLimits { get; set; } = new();
}

public class V1CtfPlayerCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;
}

public class V1CtfRateLimits
{
    [JsonPropertyName("maxInvalidFlagSubmissionsPerMinute")]
    public int MaxInvalidFlagSubmissionsPerMinute { get; set; } = 5;
    
    [JsonPropertyName("maxInvalidFlagSubmissionsPerHour")]
    public int MaxInvalidFlagSubmissionsPerHour { get; set; } = 10;
    
    [JsonPropertyName("maxInvalidFlagSubmissionsPerDay")]
    public int MaxInvalidFlagSubmissionsPerDay { get; set; } = 25;
}