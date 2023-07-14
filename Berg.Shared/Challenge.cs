using System.Text.Json.Serialization;

namespace Berg.Shared;

public class Challenge
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("author")]
    public string Author { get; set; } = null!;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = null!;
    
    [JsonPropertyName("attachments")]
    public List<Attachment> Attachments { get; set; } = new();
    
    [JsonPropertyName("value")]
    public int Value { get; set; }
    
    [JsonPropertyName("playerSolves")]
    public List<PlayerSolve> PlayerSolves { get; set; } = new();
    
    [JsonPropertyName("teamSolves")]
    public List<TeamSolve> TeamSolves { get; set; } = new();
}