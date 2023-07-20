namespace Berg.ChallengeServer.Configuration;

public class DiscordConfig
{
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string BotToken { get; set; } = null!;
    public ulong NotificationGuildId { get; set; }
    public ulong NotificationChannelId { get; set; }
}