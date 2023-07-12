namespace Berg.ChallengeServer.Db;

public class Solve
{
    public Guid Id { get; set; }
    public DateTime SolvedAt { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; }
    public string ChallengeId { get; set; } = null!;
    public Challenge Challenge { get; set; }
}