using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class AuthenticationController : ControllerBase
{
    [HttpGet]
    [Route("/api/v1/login")]
    public IActionResult Login(CancellationToken cancel)
    {
        return Challenge(new AuthenticationProperties { RedirectUri = "/" });
    }
    
    [HttpGet]
    [Route("/api/v1/logout")]
    public IActionResult GetPlayerRanking(Guid? playerId, CancellationToken cancel)
    {
        return SignOut(new AuthenticationProperties { RedirectUri = "/" });
    }
}