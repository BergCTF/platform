using Berg.DTO;
using Berg.Middleware;
using Berg.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Challenges : PageModel
{
    private readonly ChallengeService _challengeService;
    private readonly ScoreService _scoreService;

    public Dictionary<string, List<Challenge>> ChallengesByCategory = new();
    public Dictionary<Guid, ScoredChallenge> ScoredChallenges = new();
    public CachedPlayer? Player;

    public Challenges(ChallengeService challengeService, ScoreService scoreService)
    {
        _challengeService = challengeService;
        _scoreService = scoreService;
    }
    
    public async Task OnGet(CancellationToken cancellationToken)
    {
        Player = HttpContext.GetCachedPlayerOrDefault();
        ChallengesByCategory = (await _challengeService.GetChallenges(Player, cancellationToken))
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
        ScoredChallenges = _scoreService.GetScoredChallenges();
    }
}