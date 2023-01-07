namespace Berg.Middleware;

public class PlayerRegistrationMiddleware
{
    public const string CachedPlayerKey = "cachedPlayer";

    private readonly RequestDelegate _next;

    public PlayerRegistrationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        var request = context.Request;
        var response = context.Response;
        if (user.Identity?.IsAuthenticated ?? false)
        {
            var cachedPlayer = context.HasCachedPlayer() ?
                context.GetCachedPlayer() : context.InitializeCachedPlayer();

            if (!cachedPlayer.IsRegistered && !(request.Path.StartsWithSegments("/register") || 
                                               request.Path.StartsWithSegments("/account/logout") ||
                                               request.Path.StartsWithSegments("/account/select-category")))
            {
                response.Redirect("/register?redirect="+request.Path.ToUriComponent());
                return;
            }
        }
        await _next(context);
    }
}