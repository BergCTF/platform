using System.ComponentModel.DataAnnotations;

namespace Berg.Api.Db;

public class Instance
{
    [Key]
    public Guid Id { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? TerminatedAt { get; set; }
    public InstanceTerminationReason? TerminationReason { get; set; }

    public Player Player { get; set; } = default!;
    public Guid PlayerId { get; set; } = default!;

    [MaxLength(64)]
    public string ChallengeName { get; set; } = default!;
    public Challenge Challenge { get; set; } = default!;

    [MaxLength(1024)]
    public string? DynamicFlag { get; set; }
}

public enum InstanceTerminationReason {
    UserRequest,
    Timeout
}