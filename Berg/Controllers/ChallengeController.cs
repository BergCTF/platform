using System.Security.Claims;
using Berg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Controllers;

[ApiController]
[Route("/challenge")]
public class ChallengeController : ControllerBase
{
    private readonly ChallengeService _challengeService;
    
    public ChallengeController(ChallengeService challengeService)
    {
        _challengeService = challengeService;
    }
    
    [Authorize]
    [HttpPost("start", Name = "Start")]
    public async Task<IActionResult> Start([FromForm] string challengeId, CancellationToken cancellationToken)
    {
        if (User.Identity is not { IsAuthenticated: true })
            return Redirect("/error");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        
        await _challengeService.CreatePrivateInstance(userId, challengeId, cancellationToken);
        return Redirect("/challenges");
    }
    
    [Authorize]
    [HttpPost("kill", Name = "Kill")]
    public async Task<IActionResult> Kill([FromForm] string challengeId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        await _challengeService.KillPrivateInstance(userId, challengeId, cancellationToken);
        
        return Redirect("/challenges");
    }
}