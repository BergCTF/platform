using System.ComponentModel.DataAnnotations;

namespace Berg.Api.Db;

public class Instance
{
    [Key]
    public Guid Id { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime StoppedAt { get; set; }

    public Player Player { get; set; } = default!;

    public Challenge Challenge { get; set; } = default!;

    [MaxLength(1024)]
    public string? DynamicFlag { get; set; }
}