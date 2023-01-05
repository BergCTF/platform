namespace Berg.Options;

public class ChallengeInfo
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Type { get; set; } = null!;
    public List<ContainerInfo>? Containers { get; set; }
    public List<string>? AttachmentLinks { get; set; }
}