using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.Shared;
using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Services;

public class ScoringService
{
    private static readonly object CacheUpdateLock = new();
    
    private readonly CtfConfig _ctfConfig;
    
    private Dictionary<string, int> _challengeSolves = new();
    private Dictionary<string, int> _challengeValues = new();
    private Dictionary<Guid, List<PlayerSolve>> _playerSolvedChallenges = new();
    private Dictionary<Guid, List<TeamSolve>> _teamSolvedChallenges = new();
    private Dictionary<Guid, int> _playerIndividualScore = new();
    private Dictionary<Guid, int> _teamScore = new();
    private List<TeamRanking> _teamScoreboard = new();
    private List<PlayerRanking> _playerScoreboard = new();

    public ScoringService(CtfConfig ctfConfig)
    {
        _ctfConfig = ctfConfig;
    }

    public void RefreshScores(BergDbContext dbContext)
    {
        // TODO: Take Freeze Start / End Time into account when calculating the scores.
        lock (CacheUpdateLock)
        {
            // Calculate the solves and value that a single challenge has
            // This is different if this is a team based or a single player ctf.
            if (_ctfConfig.Teams)
            {
                _challengeSolves = dbContext.Challenges
                    .Select(c => new
                    {
                        c.Name,
                        Count = c.Solves.Select(s => s.Player.Team).Distinct().Count()
                    })
                    .ToDictionary(c => c.Name, c => c.Count);
            }
            else
            {
                _challengeSolves = dbContext.Challenges
                    .Include(c => c.Solves)
                    .ToDictionary(c => c.Name, c => c.Solves.Count);
            }
            
            // Calculate the score based off the number of solves
            var minimumScore = _ctfConfig.Scoring.MinimumScore;
            var maximumScore = _ctfConfig.Scoring.MaximumScore;
            var solvesBeforeMinimum = _ctfConfig.Scoring.NumSolvesBeforeMinimum;
            var factor = (minimumScore - maximumScore) / Math.Pow(solvesBeforeMinimum, 2);
            
            _challengeValues = _challengeSolves.ToDictionary(s => s.Key, s => 
                (int)Math.Max(minimumScore, Math.Ceiling(factor * Math.Pow(s.Value, 2) + maximumScore)));
            
            // Now that the values of each challenge are set, we can calculate the individual and team scores
            _playerSolvedChallenges = dbContext.Players
                .Select(p => new
                {
                    p.Id,
                    Solves = p.Solves.Select(s => new PlayerSolve
                    {
                        PlayerId = p.Id,
                        ChallengeName = s.Challenge.Name,
                        SolvedAt = s.SolvedAt
                    }).OrderByDescending(s => s.SolvedAt).ToList()
                })
                .ToDictionary(p => p.Id, p => p.Solves);
            _playerIndividualScore = _playerSolvedChallenges.ToDictionary(p => p.Key,
                p => p.Value.Select(c => _challengeValues[c.ChallengeName]).Sum());

            var solvesByTeams = dbContext.Solves
                .Where(s => s.Player.TeamId != null)
                .GroupBy(s => s.Player.TeamId!.Value)
                .ToDictionary(s => s.Key, s => s.ToList());

            _teamSolvedChallenges = solvesByTeams
                .ToDictionary(g => g.Key, g => g.Value
                    .GroupBy(s => s.ChallengeId)
                    .Select(s => new
                    {
                        ChallengeName = s.Key,
                        Solve = s.OrderBy(s2 => s2.SolvedAt).First()
                    }).Select(s => new TeamSolve
                    {
                        ChallengeName = s.ChallengeName,
                        SolvedAt = s.Solve.SolvedAt,
                        TeamId = g.Key,
                        PlayerId = s.Solve.PlayerId
                    }).ToList()
                );

            // TODO: Make sure that we also subtract points for teams that created challenges that are too hard
            _teamScore = _teamSolvedChallenges.ToDictionary(p => p.Key,
                p => p.Value.Select(c => _challengeValues[c.ChallengeName]).Sum());
            
            _playerScoreboard = dbContext.Players.Select(t => t.Id).ToList()
                .Select(t =>
                {
                    var solves = GetPlayerSolves(t);
                    return new PlayerRanking
                    {
                        PlayerId = t,
                        Score = GetPlayerScore(t),
                        Solves = solves,
                        LastSolve = solves.Count > 0 ? solves.Select(s => s.SolvedAt).Max() : null
                    };
                })
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.LastSolve)
                .ToList();
            _teamScoreboard = dbContext.Teams.Select(t => t.Id).ToList()
                .Select(t =>
                {
                    var solves = GetTeamSolves(t);
                    return new TeamRanking
                    {
                        TeamId = t,
                        Score = GetTeamScore(t),
                        Solves = solves,
                        LastSolve = solves.Count > 0 ? solves.Select(s => s.SolvedAt).Max() : null
                    };
                })
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.LastSolve)
                .ToList();
        }
    }

    public List<TeamSolve> GetChallengeTeamSolves(string challengeName)
    {
        return _teamSolvedChallenges.Values.SelectMany(s => s)
            .Where(s => s.ChallengeName == challengeName)
            .OrderBy(s => s.SolvedAt)
            .ToList();
    }
    
    public List<PlayerSolve> GetChallengePlayerSolves(string challengeName)
    {
        return _playerSolvedChallenges.Values.SelectMany(s => s)
            .Where(s => s.ChallengeName == challengeName)
            .OrderBy(s => s.SolvedAt)
            .ToList();
    }

    public bool HasPlayerSolvedChallenge(Guid? playerId, string challengeName)
    {
        if (playerId == null)
            return false;
        return _playerSolvedChallenges.TryGetValue(playerId.Value, out var solves) &&
               solves.Any(s => s.ChallengeName == challengeName);
    }
    
    public bool HasTeamSolvedChallenge(Guid? teamId, string challengeName)
    {
        if (teamId == null)
            return false;
        return _teamSolvedChallenges.TryGetValue(teamId.Value, out var solves) &&
               solves.Any(s => s.ChallengeName == challengeName);
    }

    public List<TeamRanking> GetTeamScoreboard()
    {
        return _teamScoreboard;
    }

    public List<PlayerRanking> GetPlayerScoreboard()
    {
        return _playerScoreboard;
    }
    
    public List<PlayerSolve> GetPlayerSolves(Guid playerId)
    {
        return _playerSolvedChallenges.TryGetValue(playerId, out var solves) ? solves : new List<PlayerSolve>();
    }
    
    public List<TeamSolve> GetTeamSolves(Guid teamId)
    {
        return _teamSolvedChallenges.TryGetValue(teamId, out var solves) ? solves : new List<TeamSolve>();
    }

    public int GetChallengeValue(string challengeName)
    {
        return _challengeValues.TryGetValue(challengeName, out var value) ? value : 0;
    }
    
    public int GetPlayerScore(Guid id)
    {
        return _playerIndividualScore.TryGetValue(id, out var value) ? value : 0;
    }
    
    public int GetTeamScore(Guid id)
    {
        return _teamScore.TryGetValue(id, out var value) ? value : 0;
    }
}

