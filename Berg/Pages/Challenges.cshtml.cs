using Berg.Configuration;
using Berg.DTO;
using Berg.Middleware;
using Berg.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Challenges : PageModel
{
    private readonly ChallengeService _challengeService;
    private readonly ScoreService _scoreService;
    private readonly CtfInfo _ctfInfo;

    public Dictionary<string, List<Challenge>> ChallengesByCategory = new();
    public Dictionary<Guid, ScoredChallenge> ScoredChallenges = new();
    public CachedPlayer? Player;
    public bool NotStarted;
    public DateTime CtfStart;
    public bool AlreadyEnded;
    public DateTime CtfEnd;

    public Challenges(ChallengeService challengeService, ScoreService scoreService, CtfInfo ctfInfo)
    {
        _challengeService = challengeService;
        _scoreService = scoreService;
        _ctfInfo = ctfInfo;
    }
    
    public async Task OnGet(CancellationToken cancellationToken)
    {
        Player = HttpContext.GetCachedPlayerOrDefault();
        
        var now = DateTime.Now;
        if (_ctfInfo.CtfStart > now)
        {
            NotStarted = true;
            CtfStart = _ctfInfo.CtfStart;
            return;
        }
        if (_ctfInfo.CtfEnd < now)
        {
            AlreadyEnded = true;
            CtfEnd = _ctfInfo.CtfEnd;
            return;  
        }
        ChallengesByCategory = (await _challengeService.GetChallenges(Player, cancellationToken))
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
        ScoredChallenges = _scoreService.GetScoredChallenges();
    }
}