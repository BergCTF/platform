using Berg.Api.Configuration;
using Berg.Api.Db;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using Quartz;

namespace Berg.Api.Extensions;

public sealed class InternalBackChannelReplacement(IConfiguration configuration) :
    IOpenIddictClientHandler<OpenIddictClientEvents.HandleConfigurationResponseContext>
{
    public static OpenIddictClientHandlerDescriptor Descriptor { get; }
        = OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.HandleConfigurationResponseContext>()
            .UseSingletonHandler<InternalBackChannelReplacement>()
            .SetOrder(OpenIddictClientHandlers.Discovery.ExtractTokenEndpointClientAuthenticationMethods.Descriptor.Order + 500)
            .SetType(OpenIddictClientHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(OpenIddictClientEvents.HandleConfigurationResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only do a rewrite if the internal issuer is set
        var internalIssuer = configuration["GenericOpenId:InternalIssuer"];
        if (string.IsNullOrEmpty(internalIssuer))
            return default;

        var baseUri = new Uri(internalIssuer);
        if(context.Configuration.JwksUri != null)
            context.Configuration.JwksUri = ReplaceBaseUri(baseUri, context.Configuration.JwksUri);
        if(context.Configuration.UserinfoEndpoint != null)
            context.Configuration.UserinfoEndpoint = ReplaceBaseUri(baseUri, context.Configuration.UserinfoEndpoint);
        if(context.Configuration.IntrospectionEndpoint != null)
            context.Configuration.IntrospectionEndpoint = ReplaceBaseUri(baseUri, context.Configuration.IntrospectionEndpoint);
        if(context.Configuration.TokenEndpoint != null)
            context.Configuration.TokenEndpoint = ReplaceBaseUri(baseUri, context.Configuration.TokenEndpoint);
        return default;
    }

    private static Uri ReplaceBaseUri(Uri baseUri, Uri uri)
    {
        var builder = new UriBuilder(baseUri)
        {
            Path = uri.PathAndQuery
        };
        return builder.Uri;
    }
}

public static class OpenIddictBuilder
{
    public static void AddOpenIddict(this WebApplicationBuilder builder, DiscordConfig discordConfig, GenericOpenIdConfig genericOpenIdConfig)
    {
        builder.Services.AddQuartz(options =>
        {
            options.UseSimpleTypeLoader();
            options.UseInMemoryStore();
        });
        builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options => {
                // TODO: Make sure that no cookie encryption key needs to be persisted to disk
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
            });
        builder.Services.AddAuthorization();
        builder.Services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<BergDbContext>();

                options.UseQuartz();
            })
            .AddClient(options =>
            {
                options.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow();

                // TODO: Replace ephemeral keys with configurable keys to allow multiple replicas
                options.AddEphemeralEncryptionKey();
                options.AddEphemeralSigningKey();

                options.UseAspNetCore()
                    .EnableRedirectionEndpointPassthrough();

                options.UseSystemNetHttp()
                    .SetProductInformation(typeof(Program).Assembly);

                if (!string.IsNullOrEmpty(discordConfig.ClientId))
                {
                    var secret = discordConfig.ClientSecret
                                 ?? throw new InvalidOperationException("Discord:ClientSecret is required.");
                    options.UseWebProviders()
                        .AddDiscord(discord => discord
                                .SetClientId(discordConfig.ClientId)
                                .SetProviderName(Constants.Schemes.FederatedLogin)
                                .SetClientSecret(secret)
                                .AddScopes("identify", "email")
                                .SetRedirectUri(Constants.Endpoints.FederationCallback)
                        );
                }
                else if (!string.IsNullOrEmpty(genericOpenIdConfig.ClientId))
                {
                    var issuer = genericOpenIdConfig.Issuer
                                 ?? throw new InvalidOperationException("GenericOpenId:Issuer is required.");
                    var secret = genericOpenIdConfig.ClientSecret
                                 ?? throw new InvalidOperationException("GenericOpenId:ClientSecret is required.");
                    var registration = new OpenIddictClientRegistration
                    {
                        ProviderName = Constants.Schemes.FederatedLogin,
                        Issuer = new Uri(issuer),
                        ClientId = genericOpenIdConfig.ClientId,
                        ClientSecret = secret,
                        RedirectUri = new Uri(Constants.Endpoints.FederationCallback, UriKind.RelativeOrAbsolute)
                    };

                    // Only do a rewrite if the internal issuer is set
                    if (!string.IsNullOrEmpty(genericOpenIdConfig.InternalIssuer))
                    {
                        registration.ConfigurationEndpoint =
                            new Uri($"{genericOpenIdConfig.InternalIssuer}/.well-known/openid-configuration");

                        options.AddEventHandler(InternalBackChannelReplacement.Descriptor);
                    }

                    var scopes = genericOpenIdConfig.Scopes ?? [];
                    foreach (var scope in scopes)
                    {
                        registration.Scopes.Add(scope);
                    }
                    options.AddRegistration(registration);
                }
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris(Constants.Endpoints.Authorization);
                options.SetTokenEndpointUris(Constants.Endpoints.Token);
                options.SetIntrospectionEndpointUris(Constants.Endpoints.Introspect);
                options.SetLogoutEndpointUris(Constants.Endpoints.Logout);

                options.AllowImplicitFlow();
                options.AllowPasswordFlow();
                options.AllowRefreshTokenFlow();

                // TODO: Replace ephemeral keys with configurable keys to allow multiple replicas
                options.AddEphemeralEncryptionKey();
                options.AddEphemeralSigningKey();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableLogoutEndpointPassthrough()
                    .DisableTransportSecurityRequirement();
            })
            .AddValidation(options =>
            {
                options.EnableTokenEntryValidation();
                options.UseLocalServer();
                options.UseAspNetCore();
                options.AddAudiences(Constants.ClientIds.Berg);
            });
    }

    public static async Task InitializeOpenIddictApplicationsAsync(this WebApplication app, AsyncServiceScope scope)
    {
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var infraConfig = scope.ServiceProvider.GetRequiredService<InfraConfig>();

        var bergApp = new OpenIddictApplicationDescriptor
        {
            ApplicationType = OpenIddictConstants.ApplicationTypes.Web,
            ClientId = Constants.ClientIds.Berg,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.GrantTypes.Implicit,
                OpenIddictConstants.Permissions.GrantTypes.Password,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Token,
                OpenIddictConstants.Permissions.ResponseTypes.IdToken,
                OpenIddictConstants.Permissions.ResponseTypes.IdTokenToken,
            },
            RedirectUris = {
                new Uri($"https://{infraConfig.PlatformDomain}/swagger/oauth2-redirect.html"),
            }
        };
        var redirectUris = infraConfig.RedirectUris ?? [];
        foreach (var redirectUri in redirectUris)
        {
            bergApp.RedirectUris.Add(new Uri(redirectUri));
        }

        var existingBergApp = await appManager.FindByClientIdAsync(bergApp.ClientId);
        if (existingBergApp == null)
        {
            await appManager.CreateAsync(bergApp);
        }
        else
        {
            await appManager.UpdateAsync(existingBergApp, bergApp);
        }
    }
}