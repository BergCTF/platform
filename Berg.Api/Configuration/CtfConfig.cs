namespace Berg.Api.Configuration;

public class CtfConfig
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Teams { get; set; } = false;
    public bool AllowAnonymousAccess { get; set; } = true;
    public Scoring Scoring { get; set; } = new();
    public RateLimits RateLimits { get; set; } = new();
    public List<PlayerAttribute>? PlayerAttributes { get; set; }
}


public class PlayerAttribute
{
    public string Name { get; set; } = "";
    public bool Public { get; set; } = false;
    public bool Required { get; set; } = false;
    public List<string> Values { get; set; } = [];
}