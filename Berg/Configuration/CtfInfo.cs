namespace Berg.Configuration;

public class CtfInfo
{
    public string Hostname { get; set; } = null!;
    public int PrivateInstanceTimeoutMinutes { get; set; }
    public ScoringInfo Scoring { get; set; } = null!;
    public List<ChallengeInfo> Challenges { get; set; } = null!;
}