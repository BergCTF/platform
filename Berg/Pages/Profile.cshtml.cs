using System.Security.Claims;
using Berg.Configuration;
using Berg.Db;
using Berg.Discord;
using Berg.DTO;
using Berg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

[Authorize]
public class Profile : PageModel
{

    public string Name { get; set; } = null!;
    public string DiscordId { get; set; } = null!;
    public string DiscordAvatarId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Category Category { get; set; }

    private readonly BergDbContext _dbContext;
    private readonly ScoreService _scoreService;
    public readonly CtfInfo CtfInfo;
    public ScoreboardEntry ScoreboardEntry;
    
    public Profile(BergDbContext dbContext, ScoreService scoreService, CtfInfo ctfInfo)
    {
        _dbContext = dbContext;
        _scoreService = scoreService;
        CtfInfo = ctfInfo;
    }
    
    public void OnGet()
    {
        DiscordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var player = _dbContext.Players.First(p => p.DiscordId == DiscordId);
        ScoreboardEntry = _scoreService.GetScoreboard(player.Category).First(e => e.DiscordId == DiscordId);
        DiscordAvatarId = player.DiscordAvatarId;
        Name = player.Name;
        Email = player.Email;
        Category = player.Category;
    }
}