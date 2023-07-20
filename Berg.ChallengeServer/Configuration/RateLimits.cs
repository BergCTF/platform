namespace Berg.ChallengeServer.Configuration;

public class RateLimits
{
    public int MaxInvalidFlagSubmissionsPerMinute { get; set; } = 5;
    public int MaxInvalidFlagSubmissionsPerHour { get; set; } = 60;
    public int MaxInvalidFlagSubmissionsPerDay { get; set; } = 120;
}