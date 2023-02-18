namespace Berg.Configuration;

public class CtfInfo
{
    public string ChallengeServerHostname { get; set; } = null!;
    public DateTime CtfStart { get; set; }
    public DateTime CtfEnd { get; set; }
    public int PrivateInstanceTimeoutMinutes { get; set; }
    public string? ImagePullSecret { get; set; }
    public ScoringInfo Scoring { get; set; } = null!;
    public List<ChallengeInfo> Challenges { get; set; } = null!;
    public Dictionary<string, SponsorInfo> Sponsors { get; set; } = null!;
}