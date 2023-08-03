namespace Berg.ChallengeServer.Db;

public class Player
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string DiscordId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    public List<string> Labels = new();
    public DateTime CreatedAt { get; set; }
    public List<Solve> Solves { get; set; } = null!;
    public List<Submission> Submissions { get; set; } = null!;
}