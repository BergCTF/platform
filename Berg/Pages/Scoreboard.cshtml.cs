using Berg.Configuration;
using Berg.Db;
using Berg.DTO;
using Berg.Middleware;
using Berg.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Scoreboard : PageModel
{
    private readonly ScoreService _scoreService;
    public readonly CtfInfo CtfInfo;
    public Category? SelectedCategory;
    public string? DiscordId;
    public Category? PlayerCategory;
    public List<ScoreboardEntry> ScoreboardEntries = new();

    public Scoreboard(ScoreService scoreService, CtfInfo ctfInfo)
    {
        _scoreService = scoreService;
        CtfInfo = ctfInfo;
    }
    
    public void OnGet(ScoreboardCategory? category = null)
    {
        if (HttpContext.HasCachedPlayer())
        {
            var cachedPlayer = HttpContext.GetCachedPlayer();
            DiscordId = cachedPlayer.DiscordId;
            PlayerCategory = cachedPlayer.Category;
            SelectedCategory = ToCategory(category, cachedPlayer.Category!.Value);
        }
        else
        {
            SelectedCategory = ToCategory(category ?? ScoreboardCategory.Combined);
            PlayerCategory = null;
            DiscordId = null;
        }
        ScoreboardEntries = _scoreService.GetScoreboard(SelectedCategory);
    }

    private static Category? ToCategory(ScoreboardCategory? scoreboardCategory, Category? fallback = null)
    {
        if (scoreboardCategory == null)
        {
            return fallback;
        }
        return scoreboardCategory switch
        {
            ScoreboardCategory.Junior => Category.Junior,
            ScoreboardCategory.Senior => Category.Senior,
            ScoreboardCategory.Open => Category.Open,
            ScoreboardCategory.Combined => null,
            _ => throw new ArgumentOutOfRangeException(nameof(scoreboardCategory), scoreboardCategory, null)
        };
    }
}