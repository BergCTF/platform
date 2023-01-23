using System.Security.Claims;
using Berg.Db;
using Berg.Middleware;
using Berg.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("/account")]
public class AccountController : ControllerBase
{
    private readonly BergDbContext _dbContext;
    private readonly ScoreService _scoreService;

    public AccountController(BergDbContext dbContext, ScoreService scoreService)
    {
        _dbContext = dbContext;
        _scoreService = scoreService;
    }
    
    [HttpGet("login", Name = "Login")]
    public IActionResult Login(string? redirect = null)
    {
        if (User.Identity?.IsAuthenticated ?? false)
            return LocalRedirect(redirect ?? "/");
        return Challenge(new AuthenticationProperties { RedirectUri = redirect ?? "/" });
    }
    
    [Authorize]
    [HttpPost("select-category", Name = "Select Category")]
    public IActionResult SelectCategory([FromForm] Category category, [FromForm] string? redirect = null)
    {
        var cachedPlayer = HttpContext.GetCachedPlayer();
        var dbPlayer = _dbContext.Players.FirstOrDefault(p => p.DiscordId == cachedPlayer.DiscordId);
        if (dbPlayer == null)
        {
            _dbContext.Players.Add(new Player
            {
                DiscordId = cachedPlayer.DiscordId,
                DiscordAvatarId = cachedPlayer.DiscordAvatarId,
                Category = category,
                Name = cachedPlayer.Name,
                Email = cachedPlayer.Email,
                CreatedAt = DateTime.UtcNow,
            });
            _dbContext.SaveChanges();
        }
        // Do not allow category changes
        else
        {
            return LocalRedirect("/error");
        }
        
        HttpContext.RefreshCachedPlayer();
        _scoreService.RecalculateScores(_dbContext);
        
        return LocalRedirect(redirect ?? "/");
    }
    
    [Authorize]
    [HttpGet("logout", Name = "Logout")]
    public async Task<IActionResult> Logout(string? redirect = null)
    {
        await HttpContext.SignOutAsync();
        HttpContext.RemoveCachedPlayer();
        return LocalRedirect(redirect ?? "/");
    }
    
    [Authorize]
    [HttpPost("delete", Name = "Delete")]
    public async Task<IActionResult> Delete(string? redirect = null)
    {
        var discordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        await HttpContext.SignOutAsync();
        var player = _dbContext.Players.First(p => p.DiscordId == discordId);
        _dbContext.Players.Remove(player);
        await _dbContext.SaveChangesAsync();
        _scoreService.RecalculateScores(_dbContext);
        HttpContext.RemoveCachedPlayer();
        return LocalRedirect(redirect ?? "/");
    }

}