namespace Berg.Options;

public class ContainerInfo
{
    public string Image { get; set; }
    public string ContainerName { get; set; }
    public List<PortInfo> Ports { get; set; } = new();
    public Dictionary<string, string> Environment { get; set; } = new();
}