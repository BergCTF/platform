using System.Text.Json.Serialization;

namespace Berg.Shared;

public class Player
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }
}