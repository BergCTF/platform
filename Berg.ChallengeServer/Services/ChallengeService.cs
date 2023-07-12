using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Db;
using Berg.Shared;
using k8s;
using k8s.Models;
using Challenge = Berg.Shared.Challenge;

namespace Berg.ChallengeServer.Services;

public class ChallengeService
{
    private readonly GenericClient _challengeClient;
    private readonly string _namespace;

    private readonly object _refreshLock = new();
    private Dictionary<string, V1Challenge> _challenges = new();
    
    public ChallengeService(Kubernetes kubernetes)
    {
        _challengeClient = new GenericClient(kubernetes, "berg.norelect.ch", "v1", "challenges", false);
        _namespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }

    public void RefreshChallenges(BergDbContext dbContext)
    {
        lock (_refreshLock)
        {
            var challengeList = _challengeClient
                .ListNamespacedAsync<V1BergCustomResourceList<V1Challenge>>(_namespace).Result;

            _challenges = challengeList.Items
                .ToDictionary(c => c.Name(), c => c);
            
            var dbChallenges = dbContext.Challenges.ToList();
            var missingChallengeNames = _challenges.Values.Select(c => c.Name()).ToHashSet();
            missingChallengeNames.ExceptWith(dbChallenges.Select(c => c.Name));
            
            foreach (var missingChallengeName in missingChallengeNames)
            {
                dbContext.Challenges.Add(new Db.Challenge { Name = missingChallengeName });
            }

            dbContext.SaveChanges();
        }
    }

    public List<Challenge> GetChallenges()
    {
        var utcNow = DateTime.UtcNow;
        return _challenges.Values
            .Where(c => c.Spec.HideUntil == null || c.Spec.HideUntil <= utcNow)
            .Select(ToChallenge).ToList();
    }

    private static Challenge ToChallenge(V1Challenge c)
    {
        return new Challenge
        {
            Name = c.Name(),
            Author = c.Spec.Author,
            Description = c.Spec.Description,
            Attachments = c.Spec.Attachments?.Select(a => new Attachment
            {
                FileName = a.FileName,
                DownloadUrl = a.DownloadUrl,
            }).ToList() ?? new List<Attachment>(),
        };
    }
    
}