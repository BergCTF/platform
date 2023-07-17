using System.Text.Json.Serialization;

namespace Berg.Shared;

public class Player
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("discordId")]
    public string DiscordId { get; set; } = null!;
}