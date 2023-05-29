namespace Berg.ChallengeServer.Db;

public class PlayerCategory
{
    public Guid Id { get; set; }
    public List<Player> Players { get; set; }
}