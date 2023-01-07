namespace Berg.Configuration;

public class ContainerInfo
{
    public string Image { get; set; } = null!;
    public string ContainerName { get; set; } = null!;
    public List<PortInfo> Ports { get; set; } = null!;
    public Dictionary<string, string> Environment { get; set; } = new();
}