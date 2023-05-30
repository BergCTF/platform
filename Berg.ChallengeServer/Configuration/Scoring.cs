using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.Configuration;

public class Scoring
{
    [JsonPropertyName("maximumScore")]
    public int MaximumScore { get; set; } = 500;
    
    [JsonPropertyName("minimalScore")]
    public int MinimumScore { get; set; } = 100;
    
    [JsonPropertyName("solvesBeforeMinimum")]
    public int NumSolvesBeforeMinimum { get; set; } = 45;
    
    [JsonPropertyName("freezeStart")]
    public DateTime? FreezeStart { get; set; }
    
    [JsonPropertyName("freezeEnd")]
    public DateTime? FreezeEnd { get; set; }
}