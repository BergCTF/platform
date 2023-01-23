using System.Security.Claims;
using System.Text.Json;
using Berg.Db;
using Berg.Discord;

namespace Berg.Middleware;

public static class BergMiddlewareExtensions
{
    public static IApplicationBuilder UsePlayerRegistration(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PlayerRegistrationMiddleware>();
    }
    
    public static IApplicationBuilder UseCSP(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CspMiddleware>();
    }

    public static CachedPlayer? GetCachedPlayerOrDefault(this HttpContext context)
    {
        return context.HasCachedPlayer() ? context.GetCachedPlayer() : null;
    }
    
    public static CachedPlayer GetCachedPlayer(this HttpContext context)
    {
        if (!context.Session.Keys.Contains(PlayerRegistrationMiddleware.CachedPlayerKey))
            throw new ArgumentException("No cached player set");
        var stream = new MemoryStream(context.Session.Get(PlayerRegistrationMiddleware.CachedPlayerKey)!);
        return JsonSerializer.Deserialize<CachedPlayer>(stream) ??
               throw new ArgumentException("Invalid cached player data");
    }

    public static void RefreshCachedPlayer(this HttpContext context)
    {
        context.RemoveCachedPlayer();
        context.InitializeCachedPlayer();
    }
    
    public static void RemoveCachedPlayer(this HttpContext context)
    {
        context.Session.Remove(PlayerRegistrationMiddleware.CachedPlayerKey);
        context.Session.CommitAsync().Wait();
    }
    
    public static bool HasCachedPlayer(this HttpContext context)
    {
        return context.Session.Keys.Contains(PlayerRegistrationMiddleware.CachedPlayerKey);
    }
    
    public static CachedPlayer InitializeCachedPlayer(this HttpContext context)
    {
        var session = context.Session;
        var user = context.User;

        var discordUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
        
        var dbContext = context.RequestServices.GetService<BergDbContext>()!;
        var dbPlayer = dbContext.Players.FirstOrDefault(p => p.DiscordId == discordUserId);
        var cachedPlayer = new CachedPlayer
        {
            Id = dbPlayer?.Id,
            Name = user.FindFirst(ClaimTypes.Name)?.Value! + "#" +
                   user.FindFirst(DiscordClaimTypes.Discriminator)?.Value,
            Email = user.FindFirst(ClaimTypes.Email)?.Value!,
            DiscordId = discordUserId,
            DiscordAvatarId = user.FindFirst(DiscordClaimTypes.Avatar)?.Value!,
            IsRegistered = dbPlayer != null,
            Category = dbPlayer?.Category
        };

        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, cachedPlayer);
        session.Set(PlayerRegistrationMiddleware.CachedPlayerKey, stream.ToArray());

        session.CommitAsync().Wait();
        return cachedPlayer;
    }
}