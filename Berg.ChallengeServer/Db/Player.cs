namespace Berg.ChallengeServer.Db;

public class Player
{
    public Guid Id { get; set; }
    public List<Team> Teams { get; set; }
    public List<PlayerCategory> PlayerCategories { get; set; }
}