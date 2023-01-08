namespace Berg.Configuration;

public class ChallengeInfo
{
    public Guid Id { get; set; }
    public string Category { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Flag { get; set; } = null!;
    public string Type { get; set; } = null!;
    public List<ContainerInfo> Containers { get; set; } = null!;
    public List<AttachmentInfo> Attachments { get; set; } = null!;
}