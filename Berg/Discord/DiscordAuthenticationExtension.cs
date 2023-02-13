using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Berg.Configuration;
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
        .AddCookie(o => o.Cookie = new CookieBuilder()
        {
            Name = "auth",
            SecurePolicy = CookieSecurePolicy.Always,
            SameSite = SameSiteMode.Strict,
            HttpOnly = true
        })
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
                    }
                }
            };
        });
    }
}