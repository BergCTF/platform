namespace Berg.DTO;

public class ScoredChallengeSolve
{
    public string Name { get; set; } = null!;
    public string DiscordId { get; set; } = null!;
    public string? DiscordAvatarId { get; set; }
    public DateTime SolvedAt { get; set; }
}