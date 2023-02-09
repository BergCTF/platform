using Berg.Configuration;
using Berg.Db;
using Berg.Middleware;
using Berg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("/challenge")]
public class ChallengeController : ControllerBase
{
    private readonly ChallengeService _challengeService;
    private readonly ScoreService _scoreService;
    private readonly BergDbContext _dbContext;
    private readonly CtfInfo _ctfInfo;

    public ChallengeController(
        ChallengeService challengeService,
        ScoreService scoreService,
        BergDbContext dbContext,
        CtfInfo ctfInfo)
    {
        _challengeService = challengeService;
        _dbContext = dbContext;
        _scoreService = scoreService;
        _ctfInfo = ctfInfo;
    }
    
    [Authorize]
    [HttpPost("start", Name = "Start")]
    public async Task<IActionResult> Start([FromForm] Guid challengeId, CancellationToken cancellationToken)
    {
        var player = HttpContext.GetCachedPlayer();
        await _challengeService.CreatePrivateInstance(player.Id!.Value, challengeId, cancellationToken);
        return RedirectToPage("/challenge", new { challengeId });
    }
    
    [Authorize]
    [HttpPost("kill", Name = "Kill")]
    public async Task<IActionResult> Kill([FromForm] Guid challengeId, CancellationToken cancellationToken)
    {
        var player = HttpContext.GetCachedPlayer();
        await _challengeService.KillPrivateInstance(player.Id!.Value, challengeId, cancellationToken);
        return RedirectToPage("/challenge", new { challengeId });
    }
        
    [Authorize]
    [HttpPost("submit", Name = "Submit")]
    public IActionResult Submit([FromForm] Guid challengeId, [FromForm] string flag)
    {
        var player = HttpContext.GetCachedPlayer();
        var result = _scoreService.SubmitFlag(_dbContext, player, challengeId, flag);
        return RedirectToPage("/challenge", new { challengeId, result });
    }
}