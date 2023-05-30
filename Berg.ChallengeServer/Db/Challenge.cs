using System.ComponentModel.DataAnnotations;

namespace Berg.ChallengeServer.Db;

public class Challenge
{
    [Key]
    public string Name { get; set; } = null!;
    public List<Solve> Solves { get; set; }
    public List<Submission> Submissions { get; set; }
}