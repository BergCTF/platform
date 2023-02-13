namespace Berg.Db;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DiscordId { get; set; } = null!;
    public string? DiscordAvatarId { get; set; }
    public int Score { get; set; }
    public Category Category { get; set; } = Category.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Solve> Solves { get; set; } = null!;
    public List<Submission> Submissions { get; set; } = null!;
}