using System.ComponentModel.DataAnnotations;

namespace Berg.ChallengeServer.Db;

public class PlayerAttribute
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(128)]
    public string Name { get; set; } = default!;

    [MaxLength(128)]
    public string Value { get; set; } = default!;

    public Player Player { get; set; } = default!;
}
