using System.ComponentModel.DataAnnotations;

namespace Berg.ChallengeServer.Db;

public class Solve
{
    [Key]
    public Guid Id { get; set; }

    public DateTime SolvedAt { get; set; }

    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = default!;

    [MaxLength(64)]
    public string ChallengeId { get; set; } = default!;

    public Challenge Challenge { get; set; } = default!;
}