using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Controllers;

[ApiController]
[Route("/account")]
public class AccountController : ControllerBase
{
    [HttpGet("login", Name = "Login")]
    public IActionResult Login(string? redirect = null)
    {
        if (User.Identity?.IsAuthenticated ?? false)
            return Redirect(redirect ?? "/");
        return Challenge(new AuthenticationProperties() { RedirectUri = redirect ?? "/" });
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
        throw new NotImplementedException("TODO");
        await HttpContext.SignOutAsync();
        return Redirect(redirect ?? "/");
    }

}