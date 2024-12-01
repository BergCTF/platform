using System.ComponentModel.DataAnnotations;

namespace Berg.Api.Db;

public class Team
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(128)]
    public string Name { get; set; } = default!;

    [MaxLength(128)]
    public string JoinToken { get; set; } = default!;

    public List<Player> Players { get; set; } = null!;
}