using System.Security.Claims;
using Berg.Db;
using Berg.DTO;
using Berg.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Scoreboard : PageModel
{

    private readonly BergDbContext _dbContext;
    private readonly ScoreService _scoreService;
    public bool IsLoggedIn;
    public Category ScoreboardCategory = Category.Earth;
    public List<ScoreboardEntry> ScoreboardEntries = new();

    public Scoreboard(BergDbContext dbContext, ScoreService scoreService)
    {
        _dbContext = dbContext;
        _scoreService = scoreService;
    }
    
    public void OnGet()
    {
        IsLoggedIn = User.Identity?.IsAuthenticated ?? false;
        if (IsLoggedIn)
        {
            var discordUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var player = _dbContext.Players.First(p => p.DiscordId == discordUserId);
            ScoreboardCategory = player.Category;
        }
        else
        {
            ScoreboardCategory = Category.Earth;
        }
        ScoreboardEntries = _scoreService.GetScoreboard(ScoreboardCategory);
    }
}