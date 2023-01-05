namespace Berg.Options;

public class ChallengeOptions
{
    public string? Hostname { get; set; }
    public int? PrivateInstanceTimeoutMinutes { get; set; }
    public List<ChallengeInfo>? Challenges { get; set; }
}