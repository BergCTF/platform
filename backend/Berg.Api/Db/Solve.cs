using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Berg.Api.Db;

[PrimaryKey(nameof(PlayerId), nameof(ChallengeId))]
public class Solve
{
    public Guid PlayerId { get; set; }

    [MaxLength(64)]
    public string ChallengeId { get; set; } = default!;

    public DateTime SolvedAt { get; set; }

    public Player Player { get; set; } = default!;

    public Challenge Challenge { get; set; } = default!;
}