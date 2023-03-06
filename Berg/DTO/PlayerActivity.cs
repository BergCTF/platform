using Berg.Db;

namespace Berg.DTO;

public class PlayerActivity
{
    public string PlayerName { get; set; } = null!;
    public Category PlayerCategory { get; set; }
    public string ChallengeName { get; set; } = null!;
    public Guid ChallengeId { get; set; }
    public string DiscordId { get; set; } = null!;
    public string? DiscordAvatarId { get; set; }
    public DateTime SolvedAt { get; set; }
    public bool FirstBlood { get; set; }
}