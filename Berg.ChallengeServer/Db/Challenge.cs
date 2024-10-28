using System.ComponentModel.DataAnnotations;

namespace Berg.ChallengeServer.Db;

public class Challenge
{
    [Key]
    [MaxLength(64)]
    public string Name { get; set; } = default!;

    public List<Solve> Solves { get; set; } = default!;

    public List<Submission> Submissions { get; set; } = default!;
}