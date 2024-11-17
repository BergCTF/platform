using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Berg.Api.Configuration;
using Berg.Api.Db;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.IdentityModel.Tokens;
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

public static class OpenIddictBuilder
{
    public static void AddOpenIddict(this WebApplicationBuilder builder, Kubernetes kubernetes, DiscordConfig discordConfig, GenericOpenIdConfig genericOpenIdConfig)
    {
        var keyProvider = new KubernetesSecretKeyProvider(kubernetes);

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
                options.ExpireTimeSpan = TimeSpan.FromDays(14);

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

                options.AddEncryptionKey(keyProvider.ClientEncryptionKey);
                options.AddSigningKey(keyProvider.ClientSigningKey);

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

                options.AddEncryptionKey(keyProvider.ServerEncryptionKey);
                options.AddSigningKey(keyProvider.ServerSigningKey);

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

public class KubernetesSecretKeyProvider : IXmlRepository
{
    public readonly SymmetricSecurityKey ClientEncryptionKey;
    public readonly RsaSecurityKey ClientSigningKey;
    public readonly SymmetricSecurityKey ServerEncryptionKey;
    public readonly RsaSecurityKey ServerSigningKey;
    private readonly Kubernetes _kubernetes;
    private readonly string _bergNamespace;
    private readonly string _releaseName;

    public KubernetesSecretKeyProvider(Kubernetes kubernetes)
    {
        _kubernetes = kubernetes;

        _bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        _releaseName = Environment.GetEnvironmentVariable("BERG_RELEASE") ?? "berg";
        var secretName = $"{_releaseName[0..Math.Min(_releaseName.Length, 55)]}-openid";
        var secretsLoaded = false;
        var serverSigningRsa = RSA.Create(4096);
        var clientSigningRsa = RSA.Create(4096);
        ClientEncryptionKey = GenerateSymmetricSecurityKey();
        ServerEncryptionKey = GenerateSymmetricSecurityKey();
        do
        {
            try
            {
                var secret = kubernetes.ReadNamespacedSecret(secretName, _bergNamespace);
                ClientEncryptionKey = new SymmetricSecurityKey(secret.Data["clientEncryptionKey"]);
                clientSigningRsa.ImportRSAPrivateKey(secret.Data["clientSigningKey"], out _);
                ServerEncryptionKey = new SymmetricSecurityKey(secret.Data["serverEncryptionKey"]);
                serverSigningRsa.ImportRSAPrivateKey(secret.Data["serverSigningKey"], out _);
                secretsLoaded = true;
            }
            catch (HttpOperationException)
            {
                Console.Error.WriteLine("Unable to load existing openid secret keys, generating new ones");
            }
            if(!secretsLoaded) {
                try
                {
                    kubernetes.CreateNamespacedSecret(new k8s.Models.V1Secret
                    {
                        Metadata = new k8s.Models.V1ObjectMeta
                        {
                            Name = secretName
                        },
                        Data = new Dictionary<string, byte[]> {
                            { "clientEncryptionKey", ClientEncryptionKey.Key },
                            { "clientSigningKey", clientSigningRsa.ExportRSAPrivateKey() },
                            { "serverEncryptionKey", ServerEncryptionKey.Key },
                            { "serverSigningKey", serverSigningRsa.ExportRSAPrivateKey() },
                        }
                    }, _bergNamespace);
                }
                catch (HttpOperationException)
                {
                    Console.Error.WriteLine("Failed to write newly created openid keys");
                }
            }
        } while(!secretsLoaded);

        ClientSigningKey = new RsaSecurityKey(clientSigningRsa);
        ServerSigningKey = new RsaSecurityKey(serverSigningRsa);

        var protectSecretName = $"{_releaseName[0..Math.Min(_releaseName.Length, 55)]}-protect";
        var secretNames = _kubernetes.ListNamespacedSecret(_bergNamespace).Items.Select(s => s.Name()).ToHashSet();
        if(!secretNames.Contains(protectSecretName))
        {
            _kubernetes.CreateNamespacedSecret(new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Name = protectSecretName
                },
                Data = new Dictionary<string, byte[]>()
            }, _bergNamespace);
        }
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        try
        {
            return GetAllElementsCore().ToList().AsReadOnly();
        }
        catch (HttpOperationException)
        {
            return new List<XElement>().AsReadOnly();
        }
    }

    private IEnumerable<XElement> GetAllElementsCore()
    {
        var secretName = $"{_releaseName[0..Math.Min(_releaseName.Length, 55)]}-protect";
        var secret = _kubernetes.ReadNamespacedSecret(secretName, _bergNamespace);
        if (secret.Data != null) {
            foreach(var pair in secret.Data)
            {
                yield return XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(pair.Value))));
            }
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var secretName = $"{_releaseName[0..Math.Min(_releaseName.Length, 55)]}-protect";
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting)));
        var patch = $"{{\"stringData\": {{\"{friendlyName}\": \"{content}\"}}}}";
        _kubernetes.PatchNamespacedSecret(new V1Patch(patch, V1Patch.PatchType.MergePatch), secretName, _bergNamespace);
    }

    private static readonly RandomNumberGenerator RandomNumberGenerator = RandomNumberGenerator.Create();
    private static SymmetricSecurityKey GenerateSymmetricSecurityKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.GetBytes(key);
        return new SymmetricSecurityKey(key);
    }
}
