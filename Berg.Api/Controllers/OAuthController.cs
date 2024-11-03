using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using UUIDNext;

namespace Berg.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public class OAuthController(
    BergDbContext bergDbContext,
    InfraConfig infraConfig,
    DiscordConfig discordConfig,
    GenericOpenIdConfig genericOpenIdConfig) : Controller
{
    public class DiscordUserClaim
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("username")]
        public string Username { get; set; } = default!;

        [JsonPropertyName("email")]
        public string Email { get; set; } = default!;
    }

    [HttpPost]
    [Route(Constants.Endpoints.Token)]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Token()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {

            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "The request is not a valid OAuth 2.0 request"
            });
        }
        if (request.IsPasswordGrantType())
        {
            if (!Guid.TryParse(request.Username, out var userId))
            {
                var properties = new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidRequest,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The username format is invalid. Must be a guid."
                });
                return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            var apiKeyHash = Helpers.GetApiKeyHash(request.Password ?? "", userId);

            var player = bergDbContext.Players
                .SingleOrDefault(u => u.Id == userId && u.ApiKeyHash == apiKeyHash);
            if (player == null)
            {
                var properties = new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidRequest,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The username/password pair is invalid. " +
                        "Ensure that you are using your user guid as the username and " +
                        "your API key as the password."
                });

                return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var identity = CreateClaimsIdentityForPlayer(player, Constants.LoginTypes.ApiKey,
                [Constants.Roles.Player], request.GetScopes());

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        if (request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var playerId = Guid.Parse(result.Principal?.FindFirstValue(OpenIddictConstants.Claims.Subject)
                                    ?? Guid.Empty.ToString());

            var player = bergDbContext.Players.SingleOrDefault(u => u.Id == playerId);
            if (player == null)
            {
                var properties = new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidRequest,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The refresh token is invalid."
                });

                return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var loginType = result.Principal?.FindFirstValue(Constants.Claims.LoginType)
                            ?? "refresh-token";
            var identity = CreateClaimsIdentityForPlayer(player, loginType, [Constants.Roles.Player],
                request.GetScopes());

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                "The specified grant type is not implemented."
        }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet]
    [HttpPost]
    [Route(Constants.Endpoints.Authorization)]
    public async Task<IResult> Authorize(CancellationToken cancellationToken)
    {
        // Resolve the claims stored in the cookie created after the federated authentication dance.
        // If the principal cannot be found, trigger a new challenge to redirect the user to the federated login.
        //
        // For scenarios where the default authentication handler configured in the ASP.NET Core
        // authentication options shouldn't be used, a specific scheme can be specified here.
        var principal = (await HttpContext.AuthenticateAsync()).Principal;
        if (principal is null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = HttpContext.Request.GetEncodedUrl()
            };

            return Results.Challenge(properties, [Constants.Schemes.FederatedLogin]);
        }

        var playerId = Guid.Parse(principal.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var player = await bergDbContext.Players.SingleOrDefaultAsync(u => u.Id == playerId, cancellationToken);

        if (player == null)
        {
            // If we reach this case, the user has the session cookies of a user that doesn't exist in the
            // database yet/anymore.
            return Results.SignOut(new AuthenticationProperties
            {
                RedirectUri = HttpContext.Request.GetEncodedUrl()
            });
        }

        var identity = CreateClaimsIdentityForPlayer(player, Constants.LoginTypes.Federation,
            [Constants.Roles.Player], []);
        return Results.SignIn(new ClaimsPrincipal(identity), properties: null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles federated login responses.
    ///
    /// If this is the first federated login, the local user is created. If the user already exists, the user
    /// is logged in.
    /// </summary>
    /// <param name="cancellationToken">The CancellationToken of the request</param>
    /// <returns>The tokens in the requested format</returns>
    [HttpGet]
    [HttpPost]
    [Route(Constants.Endpoints.FederationCallback)]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IResult> FederationCallback(CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(Constants.Schemes.FederatedLogin);

        string? userId, username, email;
        if (string.IsNullOrEmpty(discordConfig.ClientId))
        {
            var userIdClaim = genericOpenIdConfig.Claims?.Id ??
                              throw new InvalidOperationException("Could not find `GenericOpenId:Claims:Id` claim.");
            var userNameClaim = genericOpenIdConfig.Claims?.Name ??
                                throw new InvalidOperationException("Could not find `GenericOpenId:Claims:Name` claim.");
            var userEmailClaim = genericOpenIdConfig.Claims?.Email ??
                                 throw new InvalidOperationException("Could not find `GenericOpenId:Claims:Email` claim.");
            userId = result.Principal!.FindFirstValue(userIdClaim);
            username = result.Principal!.FindFirstValue(userNameClaim);
            email = result.Principal!.FindFirstValue(userEmailClaim);
        }
        else
        {
            var userClaim = result.Principal!.FindFirst("user")!;
            var federatedUser = JsonSerializer.Deserialize<DiscordUserClaim>(userClaim.Value)!;
            userId = federatedUser.Id;
            username = federatedUser.Username;
            email = federatedUser.Email;
        }

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Problem(new ProblemDetails
            {
                Title = "Missing claim",
                Detail = "The value for the user id claim is missing."
            });
        }
        if (string.IsNullOrEmpty(username))
        {
            return Results.Problem(new ProblemDetails
            {
                Title = "Missing claim",
                Detail = "The value for the user name claim is missing."
            });
        }
        if (string.IsNullOrEmpty(email))
        {
            return Results.Problem(new ProblemDetails
            {
                Title = "Missing claim",
                Detail = "The value for the user email claim is missing."
            });
        }

        var user = await GetOrCreatePlayerByFederatedId(userId, username, email, cancellationToken);

        var identity = CreateClaimsIdentityForPlayer(user, Constants.LoginTypes.Federation,
            [Constants.Roles.Player], []);
        var properties = new AuthenticationProperties
        {
            RedirectUri = result.Properties!.RedirectUri
        };
        return Results.SignIn(new ClaimsPrincipal(identity), properties);
    }

    /// <summary>
    /// Handle user logout
    /// </summary>
    /// <param name="cancellationToken">The CancellationToken of the request</param>
    /// <returns>The logout response</returns>
    [HttpGet]
    [HttpPost]
    [Route(Constants.Endpoints.Logout)]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync();
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Logout failed",
                Detail = "User is not authenticated"
            });
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Gets a player based on a federated id or creates a new player if it doesn't yet exist
    /// </summary>
    /// <param name="federatedId">The federated id of the player</param>
    /// <param name="federatedUsername">The federated username of the player</param>
    /// <param name="federatedEmail">The federated email of the player</param>
    /// <param name="cancellationToken">The CancellationToken of the request</param>
    /// <returns>The player object corresponding to the federated id</returns>
    private async Task<Player> GetOrCreatePlayerByFederatedId(string federatedId, string federatedUsername,
        string federatedEmail, CancellationToken cancellationToken)
    {
        await bergDbContext.Database.BeginTransactionAsync(cancellationToken);
        var player = bergDbContext.Players.SingleOrDefault(u => u.FederatedId == federatedId);

        if (player == null)
        {
            player = new Player
            {
                Id = Uuid.NewNameBased(infraConfig.PlayerIdNamespace, federatedId),
                Name = federatedUsername,
                Email = federatedEmail,
                FederatedId = federatedId,
                CreatedAt = DateTime.UtcNow
            };
            bergDbContext.Players.Add(player);
        } else {
            // Update properties every login if they change in the federated idp
            player.Name = federatedUsername;
            player.Email = federatedEmail;
        }
        await bergDbContext.SaveChangesAsync(cancellationToken);
        await bergDbContext.Database.CommitTransactionAsync(cancellationToken);
        return player;
    }

    /// <summary>
    /// Populates all fields necessary in the ClaimsIdentity for token creation
    /// </summary>
    /// <param name="player">The player to create the ClaimsIdentity for</param>
    /// <param name="loginType">The type of authentication that was performed</param>
    /// <param name="roles">The list of roles that should be granted</param>
    /// <param name="requestedScopes">The list of scopes requested</param>
    /// <returns>The ClaimsIdentity for the given player</returns>
    private static ClaimsIdentity CreateClaimsIdentityForPlayer(Player player, string loginType,
        IEnumerable<string> roles, IEnumerable<string> requestedScopes)
    {
        var identity = new ClaimsIdentity(
            authenticationType: loginType,
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, player.Id.ToString());
        identity.SetClaim(OpenIddictConstants.Claims.Name, player.Name);
        identity.SetClaim(Constants.Claims.LoginType, loginType);
        identity.SetClaims(OpenIddictConstants.Claims.Role, [..roles]);
        identity.SetAudiences(Constants.ClientIds.Berg);
        var allowedScopes = new HashSet<string>
        {
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles, OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.OfflineAccess
        };
        allowedScopes.IntersectWith(requestedScopes);
        identity.SetScopes(allowedScopes);
        identity.SetDestinations(claim =>
        {
            var nowhere = Array.Empty<string>();
            var accessTokenOnly = new[] { OpenIddictConstants.Destinations.AccessToken };
            var accessTokenAndIdToken = new[]
            {
                OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken
            };
            return claim.Type switch
            {
                OpenIddictConstants.Claims.Name => accessTokenAndIdToken,
                OpenIddictConstants.Claims.PreferredUsername => accessTokenAndIdToken,
                OpenIddictConstants.Claims.Role => accessTokenAndIdToken,
                "AspNet.Identity.SecurityStamp" => nowhere,
                _ => accessTokenOnly
            };
        });

        return identity;
    }
}