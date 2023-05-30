using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class ScoringController : Controller
{
    [HttpGet]
    [Route("/api/v1/scoreboard")]
    public async Task GetScoreboard(CancellationToken cancel)
    {
        await Task.CompletedTask;
    }
}