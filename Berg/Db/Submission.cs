namespace Berg.Db;

public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public Player Player { get; set; } = null!;
    public Challenge Challenge { get; set; } = null!;
    public string Value { get; set; } = null!;
}