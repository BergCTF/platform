namespace Berg.ChallengeServer.Db;

public class Submission
{
    public Guid Id { get; set; }
    public DateTime SubmittedAt { get; set; }
    public Player Player { get; set; }
    public Challenge Challenge { get; set; }
}