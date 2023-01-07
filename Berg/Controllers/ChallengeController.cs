using System.Security.Claims;
using Berg.Db;
using Berg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Controllers;

[ApiController]
[Route("/challenge")]
public class ChallengeController : ControllerBase
{
    private readonly ChallengeService _challengeService;
    private readonly ScoreService _scoreService;
    private readonly BergDbContext _dbContext;
    
    public ChallengeController(
        ChallengeService challengeService,
        ScoreService scoreService,
        BergDbContext dbContext)
    {
        _challengeService = challengeService;
        _scoreService = scoreService;
        _dbContext = dbContext;
    }
    
    [Authorize]
    [HttpPost("start", Name = "Start")]
    public async Task<IActionResult> Start([FromForm] Guid challengeId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        
        await _challengeService.CreatePrivateInstance(userId, challengeId, cancellationToken);
        return Redirect("/challenges");
    }
    
    [Authorize]
    [HttpPost("kill", Name = "Kill")]
    public async Task<IActionResult> Kill([FromForm] Guid challengeId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        await _challengeService.KillPrivateInstance(userId, challengeId, cancellationToken);
        
        return Redirect("/challenges");
    }
    
    [Authorize]
    [HttpPost("submit", Name = "Submit")]
    public IActionResult Submit([FromForm] Guid challengeId, [FromForm] string flag)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        var result = _scoreService.SubmitFlag(_dbContext, userId, challengeId, flag);
        
        return Redirect("/challenges");
    }
}