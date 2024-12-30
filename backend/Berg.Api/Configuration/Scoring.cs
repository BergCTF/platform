namespace Berg.Api.Configuration;

public class Scoring
{
    public int MaximumScore { get; set; } = 500;
    public int MinimumScore { get; set; } = 100;
    public int NumSolvesBeforeMinimum { get; set; } = 50;
    public DateTime? FreezeStart { get; set; }
    public DateTime? FreezeEnd { get; set; }
}