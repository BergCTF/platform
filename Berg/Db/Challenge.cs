namespace Berg.Db;

public class Challenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string? Author { get; set; }
    public string Category { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Flag { get; set; } = null!;
    public int Value { get; set; }
    public List<Solve> Solves { get; set; } = null!;
    public List<Submission> Submissions { get; set; } = null!;
}