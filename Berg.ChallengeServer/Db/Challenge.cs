namespace Berg.ChallengeServer.Db;

public class Challenge
{
    public Guid Id { get; set; }
    public List<Solve> Solves { get; set; }
    public List<Submission> Submissions { get; set; }
}