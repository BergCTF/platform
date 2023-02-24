namespace Berg.DTO;

public class ScoreboardEntry
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = null!;
    public string DiscordId { get; set; } = null!;
    public string DiscordAvatarId { get; set; } = null!;
    public int Score { get; set; }
    public DateTime? LastSolveAt { get; set; }
    public HashSet<Guid> SolvedChallenges { get; set; } = new();
    public HashSet<Guid> FirstBloodedChallenges { get; set; } = new();
}