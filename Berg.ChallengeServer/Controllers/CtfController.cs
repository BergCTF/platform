using Berg.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
[Route("/api/v1/ctf")]
public class CtfController : Controller
{

    [HttpGet]
    public Ctf GetCtfs()
    {
        var utcNow = DateTime.UtcNow;
        var start = utcNow.AddDays(-1);
        var end = utcNow.AddDays(1);
        return new Ctf
        {
            Name = "todo",
            Start = start,
            End = end,
            IsActive = start <= utcNow && utcNow < end,
        };
    }
    
}