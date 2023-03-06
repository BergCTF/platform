using Berg.Db;

namespace Berg.DTO;

public class ScoreboardEntry
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = null!;
    public Category PlayerCategory { get; set; }
    public string DiscordId { get; set; } = null!;
    public string? DiscordAvatarId { get; set; }
    public int Score { get; set; }
    public DateTime? LastSolveAt { get; set; }
    public HashSet<Guid> SolvedChallenges { get; set; } = new();
    public HashSet<Guid> FirstBloodedChallenges { get; set; } = new();
}