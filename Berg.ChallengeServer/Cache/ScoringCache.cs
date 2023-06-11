using Berg.ChallengeServer.Db;

namespace Berg.ChallengeServer.Cache;

public class ScoringCache
{
    private static readonly object CacheUpdateLock = new();
    
    private readonly BergDbContext _dbContext;
    
    private Dictionary<Guid, List<string>> _playerChallengeSolves = new();
    private Dictionary<Guid, int> _playerIndividualScore = new();

    public ScoringCache(BergDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void UpdateCache()
    {
        lock (CacheUpdateLock)
        {
            _playerChallengeSolves =
                _dbContext.Players.ToDictionary(p => p.Id, p => p.Solves.Select(s => s.Challenge.Name).ToList());

            //_playerIndividualScore = _dbContext.Players.ToDictionary(p => p.Id, );
        }
    }
    
    public List<string> GetPlayerChallengeSolves(Guid playerId)
    {
        return _playerChallengeSolves.TryGetValue(playerId, out var solves) ? solves : new List<string>();
    }

    public void GetChallengeScore(string challengeName)
    {
        
    }
}

