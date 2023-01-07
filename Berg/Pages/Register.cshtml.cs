using Berg.Middleware;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Register : PageModel
{
    public string? Redirect;
    public CachedPlayer CachedPlayer;
    
    public void OnGet(string? redirect = null)
    {
        Redirect = redirect;
        CachedPlayer = HttpContext.GetCachedPlayer();
    }
}