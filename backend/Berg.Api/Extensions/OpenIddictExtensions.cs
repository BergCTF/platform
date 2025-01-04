using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Services;
using k8s;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;
using Quartz;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Client.OpenIddictClientEvents;

namespace Berg.Api.Extensions;

public sealed class InternalBackChannelReplacement(IConfiguration configuration) :
    IOpenIddictClientHandler<HandleConfigurationResponseContext>
{
    public static OpenIddictClientHandlerDescriptor Descriptor { get; }
        = OpenIddictClientHandlerDescriptor.CreateBuilder<HandleConfigurationResponseContext>()
            .UseSingletonHandler<InternalBackChannelReplacement>()
            .SetOrder(OpenIddictClientHandlers.Discovery.ExtractTokenEndpointClientAuthenticationMethods.Descriptor.Order + 500)
            .SetType(OpenIddictClientHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(HandleConfigurationResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only do a rewrite if the internal issuer is set
        var internalIssuer = configuration["GenericOpenId:InternalIssuer"];
        if (string.IsNullOrEmpty(internalIssuer))
            return default;

        var baseUri = new Uri(internalIssuer);
        if(context.Configuration.JsonWebKeySetUri != null)
            context.Configuration.JsonWebKeySetUri = ReplaceBaseUri(baseUri, context.Configuration.JsonWebKeySetUri);
        if(context.Configuration.UserInfoEndpoint != null)
            context.Configuration.UserInfoEndpoint = ReplaceBaseUri(baseUri, context.Configuration.UserInfoEndpoint);
        if(context.Configuration.IntrospectionEndpoint != null)
            context.Configuration.IntrospectionEndpoint = ReplaceBaseUri(baseUri, context.Configuration.IntrospectionEndpoint);
        if(context.Configuration.TokenEndpoint != null)
            context.Configuration.TokenEndpoint = ReplaceBaseUri(baseUri, context.Configuration.TokenEndpoint);
        if(context.Configuration.AuthorizationEndpoint != null && context.Configuration.Issuer != null)
            context.Configuration.AuthorizationEndpoint = ReplaceBaseUri(context.Configuration.Issuer, context.Configuration.AuthorizationEndpoint);
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

public sealed class PatchWebSocketTokenValidationParameters : IOpenIddictValidationHandler<OpenIddictValidationEvents.ValidateTokenContext>
{
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
        = OpenIddictValidationHandlerDescriptor.CreateBuilder<OpenIddictValidationEvents.ValidateTokenContext>()
            .UseSingletonHandler<PatchWebSocketTokenValidationParameters>()
            .SetOrder(int.MinValue + 100_001)
            .SetType(OpenIddictValidationHandlerType.BuiltIn)
            .Build();

    public ValueTask HandleAsync(OpenIddictValidationEvents.ValidateTokenContext context)
    {
        // Hack because OpenIddict searches for the issuer with a wss:// prefix in websocket requests.
        context.TokenValidationParameters.ValidIssuers = context.TokenValidationParameters.ValidIssuers.Select(issuer => {
            if (issuer.StartsWith("wss://", StringComparison.InvariantCultureIgnoreCase))
            {
                return "https" + issuer[3..];
            }
            else if (issuer.StartsWith("ws://", StringComparison.InvariantCultureIgnoreCase))
            {
                return "http" + issuer[2..];
            }
            return issuer;
        }).ToList();
        return default;
    }
}

public static class OpenIddictBuilder
{
    public static void AddOpenIddict(this WebApplicationBuilder builder, Kubernetes kubernetes, DiscordConfig discordConfig, GenericOpenIdConfig genericOpenIdConfig)
    {
        var keyProvider = new KubernetesSecretKeyProvider(kubernetes);

        if ((discordConfig.GuildIdRequirement != 0 ||
            (discordConfig.AuthorGuildId != 0 && discordConfig.AuthorRoleId != 0) ||
            (discordConfig.AdminGuildId != 0 && discordConfig.AdminRoleId != 0)) && string.IsNullOrEmpty(discordConfig.BotToken))
        {
            throw new InvalidOperationException("Discord bot token must be provided if discord role mapping is configured.");
        }

        builder.Services.AddDataProtection()
            .SetApplicationName("Berg");
        builder.Services.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = keyProvider;
        });
        builder.Services.AddQuartz(options =>
        {
            options.UseSimpleTypeLoader();
            options.UseInMemoryStore();
        });
        builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options => {
                options.Cookie.Name = "berg-idp-session";
                options.Cookie.Path = Constants.Endpoints.BasePath;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = Constants.Lifetimes.FederatedLoginCacheLifetime;

                // Hacky workaround due to https://github.com/dotnet/aspnetcore/issues/9039
                options.Events = new CookieAuthenticationEvents()
                {
                    OnRedirectToLogin = (ctx) =>
                    {
                        ctx.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = (ctx) =>
                    {
                        ctx.Response.StatusCode = 403;
                        return Task.CompletedTask;
                    }
                };
            });
        builder.Services.AddAuthorization(options => {
            options.AddPolicy(Constants.Policies.Anonymous, policy =>
                policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                    .RequireAssertion(ctx => true));
            options.AddPolicy(Constants.Policies.Player, policy =>
                policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(Claims.Role, [Constants.Roles.Player, Constants.Roles.Author, Constants.Roles.Admin]));
            options.AddPolicy(Constants.Policies.Author, policy =>
                policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(Claims.Role, [Constants.Roles.Author, Constants.Roles.Admin]));
            options.AddPolicy(Constants.Policies.Admin, policy =>
                policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(Claims.Role, [Constants.Roles.Admin]));
        });
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

                options.AddEncryptionKey(keyProvider.ClientEncryptionKey);
                options.AddSigningKey(keyProvider.ClientSigningKey);

                options.UseAspNetCore()
                    .EnableRedirectionEndpointPassthrough();

                options.UseSystemNetHttp()
                    .SetProductInformation(typeof(Program).Assembly);

                if (!string.IsNullOrEmpty(discordConfig.ClientId))
                {
                    var registration = new OpenIddictClientRegistration()
                    {
                        ProviderName = Constants.Schemes.FederatedLogin,
                        Issuer = new Uri("https://discord.com/"),
                        ClientId = discordConfig.ClientId,
                        ClientSecret = discordConfig.ClientSecret
                                    ?? throw new InvalidOperationException("Discord:ClientSecret is required."),
                        Configuration = new OpenIddictConfiguration()
                        {
                            Issuer = new Uri("https://discord.com/"),
                            AuthorizationEndpoint = new Uri("https://discord.com/oauth2/authorize"),
                            RevocationEndpoint = new Uri("https://discord.com/api/oauth2/token/revoke"),
                            TokenEndpoint = new Uri("https://discord.com/api/oauth2/token"),
                            UserInfoEndpoint = new Uri("https://discord.com/api/users/@me")
                        },
                        RedirectUri = new Uri(Constants.Endpoints.FederationCallback, UriKind.RelativeOrAbsolute)
                    };
                    registration.Configuration.CodeChallengeMethodsSupported.Add("S256");
                    registration.Configuration.ResponseTypesSupported.Add("code");
                    registration.Configuration.GrantTypesSupported.Add("authorization_code");
                    registration.Configuration.ScopesSupported.Add("identify");
                    registration.Configuration.ScopesSupported.Add("email");
                    registration.Scopes.Add("identify");
                    registration.Scopes.Add("email");
                    options.AddRegistration(registration);
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
                options.SetEndSessionEndpointUris(Constants.Endpoints.EndSession);
                options.SetUserInfoEndpointUris(Constants.Endpoints.UserInfo);

                options.SetAccessTokenLifetime(Constants.Lifetimes.AccessTokenLifetime);
                options.SetRefreshTokenLifetime(Constants.Lifetimes.RefreshTokenLifetime);

                options.AllowImplicitFlow();
                options.AllowPasswordFlow();
                options.AllowRefreshTokenFlow();
                options.AllowAuthorizationCodeFlow();

                options.AddEncryptionKey(keyProvider.ServerEncryptionKey);
                options.AddSigningKey(keyProvider.ServerSigningKey);

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .DisableTransportSecurityRequirement();
            })
            .AddValidation(options =>
            {
                options.EnableTokenEntryValidation();
                options.UseLocalServer();
                options.UseAspNetCore(options => {
                    options.DisableAccessTokenExtractionFromBodyForm();
                });
                options.AddEventHandler(PatchWebSocketTokenValidationParameters.Descriptor);
                options.AddAudiences(Constants.ClientIds.Berg);
            });
    }

    public static async Task InitializeOpenIddictApplicationsAsync(this WebApplication app, AsyncServiceScope scope)
    {
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var infraConfig = scope.ServiceProvider.GetRequiredService<InfraConfig>();

        var bergApp = new OpenIddictApplicationDescriptor
        {
            ApplicationType = ApplicationTypes.Web,
            ClientId = Constants.ClientIds.Berg,
            ClientType = ClientTypes.Public,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.Implicit,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Token,
                Permissions.ResponseTypes.IdToken,
                Permissions.ResponseTypes.IdTokenToken,
                Permissions.ResponseTypes.Code,
            },
            RedirectUris = {
                new Uri($"https://{infraConfig.PlatformDomain}/swagger/oauth2-redirect.html"),
                new Uri($"https://{infraConfig.PlatformDomain}/frontend/oidc-callback")
            },
            PostLogoutRedirectUris = {
                new Uri($"https://{infraConfig.PlatformDomain}")
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
