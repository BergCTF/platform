namespace Berg.Api.Configuration;

public class DiscordConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string BotToken { get; set; } = "";
    public ulong NotificationGuildId { get; set; }
    public ulong NotificationChannelId { get; set; }
    public ulong PlayerGuildId { get; set; }
    public ulong PlayerRoleId { get; set; }
    public ulong AuthorGuildId { get; set; }
    public ulong AuthorRoleId { get; set; }
    public ulong AdminGuildId { get; set; }
    public ulong AdminRoleId { get; set; }
}