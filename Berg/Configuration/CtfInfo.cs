namespace Berg.Configuration;

public class CtfInfo
{
    public string ChallengeServerHostname { get; set; } = null!;
    public string CtfServerHostname { get; set; } = null!;
    public int PrivateInstanceTimeoutMinutes { get; set; }
    public ScoringInfo Scoring { get; set; } = null!;
    public List<ChallengeInfo> Challenges { get; set; } = null!;
}