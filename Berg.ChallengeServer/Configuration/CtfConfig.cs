using Berg.Shared;

namespace Berg.ChallengeServer.Configuration;

public class CtfConfig
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string ChallengeDomain { get; set; } = "localhost";
    public string GatewayName { get; set; } = "challenge-gateway";
    public bool Teams { get; set; } = false;
    public Scoring Scoring { get; set; } = new();
    public RateLimits RateLimits { get; set; } = new();
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(15);
    public int ChallengeInstanceEntryPointPort { get; set; } = 1337;
    public string ChallengeInstanceEntryPointName { get; set; } = "services";
    public List<int> AllowedOutboundPorts { get; set; } = new() { 80, 443, 4444 };
    public TimeSpan ChallengeInstanceTimeout { get; set; } = TimeSpan.FromHours(2);
    public List<PlayerAttribute>? PlayerAttributes { get; set; }
}