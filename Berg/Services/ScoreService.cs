using Berg.Configuration;
using Berg.Db;
using Berg.DTO;
using Microsoft.EntityFrameworkCore;
using Challenge = Berg.Db.Challenge;

namespace Berg.Services;

public class ScoreService
{
    private readonly object _scoreUpdateLock = new();
    private readonly CtfInfo _ctfInfo;
    private readonly Dictionary<Category, List<ScoreboardEntry>> _scoresByCategory = new();

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
        lock (_scoreUpdateLock)
        {
            return _scoresByCategory[category];
        }
    }

    public SubmissionResult SubmitFlag(BergDbContext dbContext, string discordUserId, Guid challengeId, string flag)
    {
        var player = dbContext.Players
                       .Include(u => u.Submissions)
                       .FirstOrDefault(p => p.DiscordId == discordUserId) ?? 
            throw new ArgumentException("Invalid userId");

        var challenge = dbContext.Challenges.FirstOrDefault(c => c.Id == challengeId) ?? 
            throw new ArgumentException("Invalid challengeId");

        var lastMinute = DateTime.UtcNow.AddMinutes(-1);
        var numFailedSubmissions = player.Submissions.Count(s => s.SubmittedAt > lastMinute);
        
        if (numFailedSubmissions >= _ctfInfo.Scoring.MaxFailedFlagsPerMinute)
            return SubmissionResult.RateLimited;
        
        if (flag.Trim() == challenge.Flag)
        {
            dbContext.Solves.Add(new Solve
            {
                Player = player,
                Challenge = challenge,
                SolvedAt = DateTime.UtcNow
            });
            dbContext.SaveChanges();
            RecalculateScores(dbContext);
            return SubmissionResult.Accepted;
        }
        
        dbContext.Submissions.Add(new Submission
        {
            Player = player,
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
        
            // dbContext.Challenges
            //     .ExecuteUpdate(s =>
            //         s.SetProperty(c => c.Value, c =>
            //             (int)Math.Max(
            //                 config.Minimum,
            //                 Math.Ceiling(factor * Math.Pow(c.Solves.Count, 2) + config.Initial)
            //             )
            //         )
            //     );

            foreach (var challengeSolve in dbContext
                         .Challenges.Select(c => new {Challenge = c, Solves = c.Solves.Count}))
            {
                challengeSolve.Challenge.Value = (int)Math.Max(
                    config.Minimum,
                    Math.Ceiling(factor * Math.Pow(challengeSolve.Solves, 2) + config.Initial)
                );
            }
            dbContext.SaveChanges();
            
            // dbContext.Players.ExecuteUpdate(s =>
            //     s.SetProperty(p => p.Score, p => p.Solves.Sum(sv => sv.Challenge.Value)));
            
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
                        SolvedChallenges = p.Solves.Select(s => s.Challenge.Id).ToList(),
                    })
                    .OrderByDescending(p => p.Score)
                    .ThenBy(p => p.LastSolveAt)
                    .ToList();
            }
        
            transaction.Commit();
        }
    }
}