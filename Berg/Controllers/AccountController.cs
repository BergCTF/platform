using System.Security.Claims;
using Berg.Db;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Controllers;

[ApiController]
[Route("/account")]
public class AccountController : ControllerBase
{
    private readonly BergDbContext _dbContext;

    public AccountController(BergDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    [HttpGet("login", Name = "Login")]
    public IActionResult Login(string? redirect = null)
    {
        if (User.Identity?.IsAuthenticated ?? false)
            return Redirect(redirect ?? "/");
        return Challenge(new AuthenticationProperties { RedirectUri = redirect ?? "/" });
    }
    
    [Authorize]
    [HttpGet("logout", Name = "Logout")]
    public async Task<IActionResult> Logout(string? redirect = null)
    {
        await HttpContext.SignOutAsync();
        return Redirect(redirect ?? "/");
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
        return Redirect(redirect ?? "/");
    }

}