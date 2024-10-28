using System.ComponentModel.DataAnnotations;

namespace Berg.ChallengeServer.Db;

public class Submission
{
    [Key]
    public Guid Id { get; set; }

    public DateTime SubmittedAt { get; set; }

    public Player Player { get; set; } = default!;

    public Challenge Challenge { get; set; } = default!;

    [MaxLength(1024)]
    public string Value { get; set; } = default!;
}