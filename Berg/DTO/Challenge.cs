using Berg.Configuration;

namespace Berg.DTO;

public class Challenge
{
    public Guid Id { get; set; }
    public string? Category { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public ChallengeType Type { get; set; }
    public ChallengeStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<Service> Services { get; set; } = null!;
    public List<AttachmentInfo>? Attachments { get; set; }
}
