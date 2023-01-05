namespace Berg.DTO;

public class Challenge
{
    public string Id { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public ChallengeType Type { get; set; }
    public ChallengeStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<Service>? Services { get; set; }
    public List<string>? AttachmentLinks { get; set; }
}
