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
    private readonly Dictionary<Category, List<ScoreboardEntry>> _scoresByCategory = new();
    private readonly Dictionary<Guid, ScoredChallenge> _scoredChallenges = new();

    public ScoreService(CtfInfo ctfInfo)
    {
        _ctfInfo = ctfInfo;

        foreach (var category in Enum.GetValues<Category>())
        {
            _scoresByCategory[category] = new List<ScoreboardEntry>();
        }
    }

    public List<ScoreboardEntry> GetScoreboard(Category category)
    {
        return _scoresByCategory[category];
    }

    public ScoredChallenge GetScoredChallenge(Guid challengeId)
    {
        return _scoredChallenges[challengeId];
    }

    public Dictionary<Guid, ScoredChallenge> GetScoredChallenges()
    {
        return _scoredChallenges;
    }

    public SubmissionResult SubmitFlag(BergDbContext dbContext, CachedPlayer player, Guid challengeId, string flag)
    {
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
                _scoredChallenges[challenge.Id] = new ScoredChallenge
                {
                    Id = challenge.Id,
                    Value = challenge.Value,
                    Solves = dbContext.Solves.Include(s => s.Player)
                        .Where(s => s.Challenge == challenge)
                        .Select(s => new ScoredChallengeSolve
                            {
                                Name = s.Player.Name,
                                DiscordId = s.Player.DiscordId,
                                DiscordAvatarId = s.Player.DiscordAvatarId,
                                SolvedAt = s.SolvedAt
                            })
                        .OrderBy(s => s.SolvedAt)
                        .ToList()
                };
            }
            dbContext.SaveChanges();
            
            foreach (var playerScore in dbContext
                         .Players.Select(p => new {Player = p, Score = p.Solves.Sum(s => s.Challenge.Value)}))
            {
                playerScore.Player.Score = playerScore.Score;
            }
            dbContext.SaveChanges();
            
            foreach (var category in Enum.GetValues<Category>())
            {
                _scoresByCategory[category] = dbContext.Players
                    .Where(p => p.Category == category)
                    .Select(p => new ScoreboardEntry
                    {
                        Name = p.Name,
                        DiscordId = p.DiscordId,
                        DiscordAvatarId = p.DiscordAvatarId,
                        Score = p.Solves.Sum(s => s.Challenge.Value),
                        LastSolveAt = p.Solves.Max(s => s.SolvedAt),
                        SolvedChallenges = p.Solves.Select(s => s.Challenge.Id).ToHashSet(),
                    })
                    .OrderByDescending(p => p.Score)
                    .ThenBy(p => p.LastSolveAt)
                    .ToList();
            }
        
            transaction.Commit();
        }
    }
}