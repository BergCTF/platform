using Berg.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Register : PageModel
{
    public string? Redirect;
    public CachedPlayer CachedPlayer;
    
    public IActionResult OnGet(string? redirect = null)
    {
        Redirect = redirect;
        CachedPlayer = HttpContext.GetCachedPlayer();

        if (CachedPlayer.Id.HasValue)
            return Redirect(redirect ?? "/");
        
        return Page();
    }
}