using Berg.Services;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Controllers;

[ApiController]
[Route("/score")]
public class ScoreController : ControllerBase
{
    private readonly ScoreService _scoreService;

    public ScoreController(ScoreService scoreService)
    {
        _scoreService = scoreService;
    }

    [HttpGet("junior", Name = "Junior")]
    public void Junior()
    {
        
    }

}