using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.Shared;

namespace Berg.ChallengeServer.Cache;

public class ScoringCache
{
    private static readonly object CacheUpdateLock = new();
    
    private readonly BergDbContext _dbContext;
    private readonly CtfConfig _ctfConfig;
    
    private Dictionary<string, int> _challengeSolves = new();
    private Dictionary<string, int> _challengeValues = new();
    private Dictionary<Guid, List<PlayerSolve>> _playerSolvedChallenges = new();
    private Dictionary<Guid, List<TeamSolve>> _teamSolvedChallenges = new();
    private Dictionary<Guid, int> _playerIndividualScore = new();
    private Dictionary<Guid, int> _teamScore = new();

    public ScoringCache(BergDbContext dbContext, CtfConfig ctfConfig)
    {
        _dbContext = dbContext;
        _ctfConfig = ctfConfig;
    }

    public void UpdateCache()
    {
        lock (CacheUpdateLock)
        {
            // Calculate the solves and value that a single challenge has
            // This is different if this is a team based or a single player ctf.
            if (_ctfConfig.Teams)
            {
                _challengeSolves = _dbContext.Challenges
                    .ToDictionary(c => c.Name, c => c.Solves.DistinctBy(s => s.Player.Team).Count());
            }
            else
            {
                _challengeSolves = _dbContext.Challenges.ToDictionary(c => c.Name, c => c.Solves.Count);
            }
            
            // Calculate the score based off the number of solves
            var minimumScore = _ctfConfig.Scoring.MinimumScore;
            var maximumScore = _ctfConfig.Scoring.MaximumScore;
            var solvesBeforeMinimum = _ctfConfig.Scoring.NumSolvesBeforeMinimum;
            var factor = (minimumScore - maximumScore) / Math.Pow(solvesBeforeMinimum, 2);
            
            _challengeValues = _challengeSolves.ToDictionary(s => s.Key, s => 
                (int)Math.Max(minimumScore, Math.Ceiling(factor * Math.Pow(s.Value, 2) + maximumScore)));
            
            // Now that the values of each challenge are set, we can calculate the individual and team scores
            
            _playerSolvedChallenges = _dbContext.Players
                .ToDictionary(p => p.Id, p => p.Solves.Select(s => new PlayerSolve
                {
                    ChallengeName = s.Challenge.Name,
                    SolvedAt = s.SolvedAt
                }).OrderByDescending(s => s.SolvedAt).ToList());
            _playerIndividualScore = _playerSolvedChallenges.ToDictionary(p => p.Key,
                p => p.Value.Select(c => _challengeValues[c.ChallengeName]).Sum());

            _teamSolvedChallenges = _dbContext.Teams.ToDictionary(t => t.Id,
                t => t.Players.SelectMany(p => p.Solves).GroupBy(s => s.Challenge).Select(g => new
                {
                    ChallengeName = g.Key.Name,
                    Solve = g.OrderByDescending(s => s.SolvedAt).First()
                }).Select(g => new TeamSolve
                {
                    ChallengeName = g.ChallengeName,
                    SolvedAt = g.Solve.SolvedAt,
                    PlayerId = g.Solve.Player.Id
                }).OrderByDescending(s => s.SolvedAt).DistinctBy(s => s.ChallengeName).ToList());
            
            // TODO: Make sure that we also subtract points for teams that created challenges that are too hard
            _teamScore = _teamSolvedChallenges.ToDictionary(p => p.Key,
                p => p.Value.Select(c => _challengeValues[c.ChallengeName]).Sum());
        }
    }
    
    public List<PlayerSolve> GetPlayerChallengeSolves(Guid playerId)
    {
        return _playerSolvedChallenges.TryGetValue(playerId, out var solves) ? solves : new List<PlayerSolve>();
    }

    public int GetChallengeValue(string challengeName)
    {
        if (_challengeValues.TryGetValue(challengeName, out var value))
            return value;
        return -1;
    }
    
    public int GetPlayerIndividualScore(Guid id)
    {
        if (_playerIndividualScore.TryGetValue(id, out var value))
            return value;
        return -1;
    }
    
    public int GetTeamScore(Guid id)
    {
        if (_teamScore.TryGetValue(id, out var value))
            return value;
        return -1;
    }
}

