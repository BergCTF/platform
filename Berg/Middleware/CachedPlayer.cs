using Berg.Db;

namespace Berg.Middleware;

public class CachedPlayer
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DiscordId { get; set; } = null!;
    public string DiscordAvatarId { get; set; } = null!;
    public bool IsRegistered { get; set; }
    public Category? Category { get; set; }
}