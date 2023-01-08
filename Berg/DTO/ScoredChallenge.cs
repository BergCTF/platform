namespace Berg.DTO;

public class ScoredChallenge
{
    public Guid Id { get; set; }
    public int Value { get; set; }
    public List<ScoredChallengeSolve> Solves { get; set; } = new();
}