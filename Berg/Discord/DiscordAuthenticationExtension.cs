using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Berg.Configuration;
using Berg.Db;
using Berg.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace Berg.Discord;

public static class DiscordAuthenticationExtension
{
    public static void AddDiscordAuthentication(this IServiceCollection services, DiscordAuthenticationInfo info)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = "Discord";
        })
        .AddCookie()
        .AddOAuth("Discord", options =>
        {
            options.ClientId = info.ClientId;
            options.ClientSecret = info.ClientSecret;
            options.CallbackPath = new PathString("/oauth2/callback");

            options.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize";
            options.TokenEndpoint = "https://discord.com/api/oauth2/token";
            options.UserInformationEndpoint = "https://discord.com/api/users/@me";
            options.SaveTokens = true;
            
            options.Scope.Add("identify");
            options.Scope.Add("email");

            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
            options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            options.ClaimActions.MapJsonKey(ClaimTypes.Locality, "locale");
            options.ClaimActions.MapJsonKey(DiscordClaimTypes.Discriminator, "discriminator");
            options.ClaimActions.MapJsonKey(DiscordClaimTypes.Verified, "verified");
            options.ClaimActions.MapJsonKey(DiscordClaimTypes.Avatar, "avatar");

            options.Events = new OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                    var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();
                    
                    var user = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    context.RunClaimActions(user.RootElement);

                    if (!user.RootElement.GetProperty("verified").GetBoolean())
                    {
                        context.Fail("User is not verified");
                        return;
                    }

                    await using var dbContext = context.HttpContext.RequestServices.GetService<BergDbContext>()!;
                    var scoreService = context.HttpContext.RequestServices.GetService<ScoreService>()!;
                    var discordId = user.RootElement.GetProperty("id").GetString()!;
                    var player = dbContext.Players.FirstOrDefault(p => p.DiscordId == discordId);
                    var name = user.RootElement.GetProperty("username").GetString()! + "#" +
                               user.RootElement.GetProperty("discriminator").GetString()!;
                    var email = user.RootElement.GetProperty("email").GetString()!;
                    var avatarId = user.RootElement.GetProperty("avatar").GetString()!;
                    if (player == null)
                    {
                        dbContext.Players.Add(new Player
                        {
                            DiscordId = discordId,
                            DiscordAvatarId = avatarId,
                            Name = name,
                            Email = email,
                        });
                    }
                    else
                    {
                        player.DiscordAvatarId = avatarId;
                        player.Name = name;
                        player.Email = email;
                    }
                    await dbContext.SaveChangesAsync();
                    scoreService.RecalculateScores(dbContext);
                }
            };
        });
    }
}