namespace Berg.Api.Configuration;

public class DiscordConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string BotToken { get; set; } = "";
    public ulong NotificationGuildId { get; set; }
    public ulong NotificationChannelId { get; set; }
}