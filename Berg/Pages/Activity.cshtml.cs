using Berg.Middleware;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Activity : PageModel
{
    public string? DiscordId;
    
    public void OnGet()
    {
        if (HttpContext.HasCachedPlayer())
        {
            var cachedPlayer = HttpContext.GetCachedPlayer();
            DiscordId = cachedPlayer.DiscordId;
        }
        else
        {
            DiscordId = null;
        }
    }
}