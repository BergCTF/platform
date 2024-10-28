using System.ComponentModel.DataAnnotations;

namespace Berg.Api.Db;

public class Player
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(1024)]
    public string Name { get; set; } = default!;

    [MaxLength(128)]
    public string DiscordId { get; set; } = default!;

    [MaxLength(1024)]
    public string Email { get; set; } = default!;

    public Guid? TeamId { get; set; }

    public Team? Team { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Solve> Solves { get; set; } = default!;

    public List<Submission> Submissions { get; set; } = default!;

    public List<PlayerAttribute> Attributes { get; set; } = default!;
}
