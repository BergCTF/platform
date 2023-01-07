namespace Berg.Configuration;

public class PortInfo
{
    public int Port { get; set; }
    public string Protocol { get; set; } = "tcp";
    public string AppProtocol { get; set; } = "tcp";
    public bool Exposed { get; set; } = false;
}