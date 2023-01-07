namespace Berg.Db;

public class Solve
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SolvedAt { get; set; } = DateTime.UtcNow;
    public Player Player { get; set; } = null!;
    public Challenge Challenge { get; set; } = null!;
}