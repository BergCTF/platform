namespace Berg.Api.Configuration;

public class CtfConfig
{
    public string EventName { get; set; } = "";
    public string EventOrganiser { get; set; } = "";
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
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Public { get; set; } = false;
    public bool Required { get; set; } = false;
    public List<PlayerAttributeValue> Values { get; set; } = [];
}

public class PlayerAttributeValue
{
    public string Value { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}