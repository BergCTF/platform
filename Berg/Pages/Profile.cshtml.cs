using System.Security.Claims;
using Berg.Db;
using Berg.Discord;
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

    public Profile(BergDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public void OnGet()
    {
        DiscordId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var player = _dbContext.Players.First(p => p.DiscordId == DiscordId);
        DiscordAvatarId = player.DiscordAvatarId;
        Name = player.Name;
        Email = player.Email;
        Category = player.Category;
    }
}