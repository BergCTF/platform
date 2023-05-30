namespace Berg.ChallengeServer.Db;

public class Player
{
    public Guid Id { get; set; }
    public Team? Team { get; set; }
    public PlayerCategory? PlayerCategory { get; set; }
    public List<Solve> Solves { get; set; } = null!;
    public List<Submission> Submissions { get; set; } = null!;
}