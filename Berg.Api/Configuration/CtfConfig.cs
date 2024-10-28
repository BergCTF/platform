using Berg.Shared;

namespace Berg.Api.Configuration;

public class CtfConfig
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Teams { get; set; } = false;
    public Scoring Scoring { get; set; } = new();
    public RateLimits RateLimits { get; set; } = new();
    public List<PlayerAttribute>? PlayerAttributes { get; set; }
}