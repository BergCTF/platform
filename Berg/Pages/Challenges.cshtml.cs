using Berg.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Challenges : PageModel
{
    public ChallengeService ChallengeService;
    public ScoreService ScoreService;
    
    public Challenges(ChallengeService challengeService, ScoreService scoreService)
    {
        ChallengeService = challengeService;
        ScoreService = scoreService;
    }
    
    public void OnGet()
    {
    }
}