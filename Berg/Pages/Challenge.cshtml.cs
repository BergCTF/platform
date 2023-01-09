using Berg.DTO;
using Berg.Middleware;
using Berg.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class ChallengeDetail : PageModel
{
    private readonly ChallengeService _challengeService;
    private readonly ScoreService _scoreService;

    public new DTO.Challenge Challenge = null!;
    public CachedPlayer? Player;
    public ScoredChallenge ScoredChallenge = null!;
    public SubmissionResult? SubmissionResult;

    public ChallengeDetail(ChallengeService challengeService, ScoreService scoreService)
    {
        _challengeService = challengeService;
        _scoreService = scoreService;
    }
    
    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken,
        Guid? challengeId = null,
        SubmissionResult? result = null)
    {
        if (!challengeId.HasValue)
            return Redirect("/challenges");

        SubmissionResult = result;
        Player = HttpContext.GetCachedPlayerOrDefault();
        Challenge = await _challengeService.GetChallenge(Player, challengeId.Value, cancellationToken);
        ScoredChallenge = _scoreService.GetScoredChallenge(challengeId.Value);
        return Page();
    }
}