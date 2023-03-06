using System.Text.RegularExpressions;
using Berg.Configuration;
using Berg.Db;
using Berg.DTO;
using Berg.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Berg.Services;

public class ScoreService
{
    private readonly object _scoreUpdateLock = new();
    private readonly CtfInfo _ctfInfo;
    private readonly Dictionary<Guid, ScoredChallenge> _scoredChallenges = new();
    private List<ScoreboardEntry> _scoreboard = new();
    private List<PlayerActivity> _latestPlayerActivities = new();

    public ScoreService(CtfInfo ctfInfo)
    {
        _ctfInfo = ctfInfo;
    }

    public List<ScoreboardEntry> GetScoreboard(Category? category = null)
    {
        return category == null ? _scoreboard : _scoreboard
            .Where(e => e.PlayerCategory == category)
            .ToList();
    }

    public ScoredChallenge GetScoredChallenge(Guid challengeId)
    {
        return _scoredChallenges[challengeId];
    }

    public Dictionary<Guid, ScoredChallenge> GetScoredChallenges()
    {
        return _scoredChallenges;
    }

    public List<PlayerActivity> GetLatestPlayerActivities()
    {
        return _latestPlayerActivities;
    }

    public SubmissionResult SubmitFlag(BergDbContext dbContext, CachedPlayer player, Guid challengeId, string flag)
    {
        var now = DateTime.Now;
        if (_ctfInfo.CtfStart > now || _ctfInfo.CtfEnd < now)
        {
            return SubmissionResult.CtfNotActive;
        }
        
        var dbPlayer = dbContext.Players
                       .Include(u => u.Submissions)
                       .FirstOrDefault(p => p.DiscordId == player.DiscordId) ?? 
            throw new ArgumentException("Invalid userId");

        var challenge = dbContext.Challenges.FirstOrDefault(c => c.Id == challengeId) ?? 
            throw new ArgumentException("Invalid challengeId");

        var lastMinute = DateTime.UtcNow.AddMinutes(-1);
        var numFailedSubmissions = dbPlayer.Submissions.Count(s => s.SubmittedAt > lastMinute);
        
        if (numFailedSubmissions >= _ctfInfo.Scoring.MaxFailedFlagsPerMinute)
            return SubmissionResult.RateLimited;
        
        if (flag.Trim() == challenge.Flag)
        {
            if (dbContext.Solves.Any(s => s.Player == dbPlayer && s.Challenge == challenge))
                return SubmissionResult.AlreadySubmitted;
            
            dbContext.Solves.Add(new Solve
            {
                Player = dbPlayer,
                Challenge = challenge,
                SolvedAt = DateTime.UtcNow
            });
            dbContext.SaveChanges();
            RecalculateScores(dbContext);
            return SubmissionResult.Accepted;
        }
        
        dbContext.Submissions.Add(new Submission
        {
            Player = dbPlayer,
            Challenge = challenge,
            Value = flag,
            SubmittedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
        return SubmissionResult.Rejected;
    }

    internal void RecalculateScores(BergDbContext dbContext)
    {
        var config = _ctfInfo.Scoring;
        var factor = (config.Minimum - config.Initial) / Math.Pow(config.NumSolvesBeforeMinimum, 2);

        lock (_scoreUpdateLock)
        {
            using var transaction = dbContext.Database.BeginTransaction();

            foreach (var challengeSolve in dbContext.Challenges
                         .Select(c => new {Challenge = c, Solves = c.Solves.Count})
                         .ToList())
            {
                var challenge = challengeSolve.Challenge;
                challenge.Value = (int)Math.Max(
                    config.Minimum,
                    Math.Ceiling(factor * Math.Pow(challengeSolve.Solves, 2) + config.Initial)
                );

                var solves = dbContext.Solves.Include(s => s.Player)
                    .Where(s => s.Challenge == challenge)
                    .Select(s => new ScoredChallengeSolve
                    {
                        PlayerName = s.Player.Name,
                        PlayerCategory = s.Player.Category,
                        DiscordId = s.Player.DiscordId,
                        DiscordAvatarId = s.Player.DiscordAvatarId,
                        SolvedAt = s.SolvedAt
                    })
                    .OrderBy(s => s.SolvedAt)
                    .ToList();
                
                foreach (var entry in solves)
                {
                    entry.PlayerName = CensorName(entry.PlayerName);
                }
                
                _scoredChallenges[challenge.Id] = new ScoredChallenge
                {
                    Id = challenge.Id,
                    Value = challenge.Value,
                    Solves = solves
                };
            }
            dbContext.SaveChanges();
            
            foreach (var playerScore in dbContext
                         .Players.Select(p => new {Player = p, Score = p.Solves.Sum(s => s.Challenge.Value)}))
            {
                playerScore.Player.Score = playerScore.Score;
            }
            dbContext.SaveChanges();

            _scoreboard = dbContext.Players
                .Select(p => new ScoreboardEntry
                {
                    PlayerId = p.Id,
                    PlayerName = p.Name,
                    PlayerCategory = p.Category,
                    DiscordId = p.DiscordId,
                    DiscordAvatarId = p.DiscordAvatarId,
                    Score = p.Solves.Sum(s => s.Challenge.Value),
                    LastSolveAt = p.Solves.Max(s => s.SolvedAt),
                    SolvedChallenges = p.Solves.Select(s => s.Challenge.Id).ToHashSet(),
                })
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.LastSolveAt)
                .ThenBy(p => p.DiscordId)
                .ToList();
            
            foreach (var category in Enum.GetValues<Category>())
            {
                var firstBloods = dbContext.Solves
                    .Where(s => s.Player.Category == category)
                    .Select(s => new
                    {
                        ChallengeId = s.Challenge.Id,
                        PlayerId = s.Player.Id,
                        SolvedAt = s.SolvedAt,
                    })
                    .GroupBy(s => s.ChallengeId)
                    .ToDictionary(g => g.Key, g => g.ToList())
                    .ToDictionary(s => s.Key, s => s.Value.MinBy(v => v.SolvedAt)!.PlayerId);

                foreach (var entry in _scoreboard.Where(e => e.PlayerCategory == category))
                {
                    entry.FirstBloodedChallenges = entry.SolvedChallenges
                            .Where(c => firstBloods[c] == entry.PlayerId).ToHashSet();
                }
            }
            
            foreach (var entry in _scoreboard)
            {
                entry.PlayerName = CensorName(entry.PlayerName);
            }

            _latestPlayerActivities = dbContext.Solves
                .Include(s => s.Player)
                .Include(s => s.Challenge)
                .OrderByDescending(s => s.SolvedAt)
                .Take(25)
                .ToList()
                .Select(s => new PlayerActivity
                {
                    SolvedAt = s.SolvedAt,
                    ChallengeId = s.Challenge.Id,
                    DiscordId = s.Player.DiscordId,
                    DiscordAvatarId = s.Player.DiscordAvatarId,
                    ChallengeName = s.Challenge.Name,
                    PlayerName = CensorName(s.Player.Name),
                    PlayerCategory = s.Player.Category,
                    FirstBlood = false
                })
                .ToList();
        
            transaction.Commit();
        }
    }

    private static string CensorName(string name)
    {
        return Regex.Replace(name, @"#\d{4}$", "");
    }
}