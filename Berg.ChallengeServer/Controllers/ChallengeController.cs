using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
[Route("[controller]")]
public class ChallengeController : ControllerBase
{
    private readonly ILogger<ChallengeController> _logger;

    public ChallengeController(ILogger<ChallengeController> logger)
    {
        _logger = logger;
    }

}