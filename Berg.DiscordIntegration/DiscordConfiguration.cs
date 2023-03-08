namespace Berg.DiscordIntegration;

public class DiscordConfiguration
{
    public string? BotToken { get; set; }
    public ulong ActivityChannelId { get; set; }
    public ulong GuildId { get; set; }
    public int LookBehindMinutes { get; set; } = 5;
}