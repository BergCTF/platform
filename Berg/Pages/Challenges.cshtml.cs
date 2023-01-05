using Berg.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Challenges : PageModel
{
    public ChallengeService _challengeService;
    
    public Challenges(ChallengeService challengeService)
    {
        _challengeService = challengeService;
    }
    
    public void OnGet()
    {
    }
}