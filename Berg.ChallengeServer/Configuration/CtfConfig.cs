namespace Berg.ChallengeServer.Configuration;

public class CtfConfig
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string ChallengeDomain { get; set; } = "localhost";
    public bool Teams { get; set; } = false;
    public Scoring Scoring { get; set; } = new();
    public RateLimits RateLimits { get; set; } = new();
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan ChallengeInstanceTimeout { get; set; } = TimeSpan.FromHours(1);
}