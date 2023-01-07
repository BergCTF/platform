namespace Berg.Configuration;

public class ScoringInfo
{
    public int Initial { get; set; }
    public int Minimum { get; set; }
    public int NumSolvesBeforeMinimum { get; set; }
    public int MaxFailedFlagsPerMinute { get; set; }
}