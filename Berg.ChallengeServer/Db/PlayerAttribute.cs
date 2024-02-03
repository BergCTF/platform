namespace Berg.ChallengeServer.Db;

public class PlayerAttribute
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public Player Player { get; set; } = null!;
}
