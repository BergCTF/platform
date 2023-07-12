using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.Configuration;

public class DiscordConfig
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = null!;
    
    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = null!;
    
    [JsonPropertyName("botToken")]
    public string BotToken { get; set; } = null!;
    
    [JsonPropertyName("notificationGuildId")]
    public ulong NotificationGuildId { get; set; }
    
    [JsonPropertyName("notificationChannelId")]
    public ulong NotificationChannelId { get; set; }
}