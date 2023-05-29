using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.Configuration;

public class PlayerCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;
}