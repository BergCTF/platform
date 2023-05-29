namespace Berg.ChallengeServer.Db;

public class Solve
{
    public Guid Id { get; set; }
    public DateTime SolvedAt { get; set; }
    public Player Player { get; set; }
    public Challenge Challenge { get; set; }
}