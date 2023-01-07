using System.Security.Claims;
using Berg.Configuration;
using Berg.Db;
using Berg.DTO;
using Berg.Middleware;
using Berg.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Scoreboard : PageModel
{

    private readonly BergDbContext _dbContext;
    private readonly ScoreService _scoreService;
    public readonly CtfInfo CtfInfo;
    public Category ScoreboardCategory = Category.Open;
    public string? DiscordId = null;
    public Category? PlayerCategory = null;
    public List<ScoreboardEntry> ScoreboardEntries = new();

    public Scoreboard(BergDbContext dbContext, ScoreService scoreService, CtfInfo ctfInfo)
    {
        _dbContext = dbContext;
        _scoreService = scoreService;
        CtfInfo = ctfInfo;
    }
    
    public void OnGet(Category? category = null)
    {
        if (HttpContext.HasCachedPlayer())
        {
            var cachedPlayer = HttpContext.GetCachedPlayer();
            DiscordId = cachedPlayer.DiscordId;
            PlayerCategory = cachedPlayer.Category;
            ScoreboardCategory = category ?? cachedPlayer.Category!.Value;
        }
        else
        {
            ScoreboardCategory = category ?? Category.Open;
            PlayerCategory = null;
            DiscordId = null;
        }
        ScoreboardEntries = _scoreService.GetScoreboard(ScoreboardCategory);
    }
}