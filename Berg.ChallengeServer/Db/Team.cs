namespace Berg.ChallengeServer.Db;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string JoinToken { get; set; } = null!;
    public List<Player> Players { get; set; } = null!;
}