using System.Diagnostics;

namespace Berg.Api;

public static class Constants
{
    public readonly static ActivitySource BergActivitySource = new("Berg.Api");

    /// <summary>
    /// Role constants
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// The name of the player role
        /// </summary>
        public const string Player = "player";

        /// <summary>
        /// The name of the author role
        /// </summary>
        public const string Author = "author";

        /// <summary>
        /// The name of the admin role
        /// </summary>
        public const string Admin = "admin";
    }

    /// <summary>
    /// Policy constants
    /// </summary>
    public static class Policies
    {
        /// <summary>
        /// The name of the anonymous policy
        /// </summary>
        public const string Anonymous = "anonymous";

        /// <summary>
        /// The name of the player policy
        /// </summary>
        public const string Player = "player";

        /// <summary>
        /// The name of the author policy
        /// </summary>
        public const string Author = "author";

        /// <summary>
        /// The name of the admin policy
        /// </summary>
        public const string Admin = "admin";
    }

    /// <summary>
    /// Claims constants
    /// </summary>
    public static class Claims
    {
        private const string Prefix = "berg_";

        /// <summary>
        /// The name of the LoginType claim
        /// </summary>
        public const string LoginType = Prefix + "login_type";

        /// <summary>
        /// The name of the DiscordMappedRoles claim
        /// </summary>
        public const string DiscordMappedRoles = Prefix + "discord_mapped_roles";
    }

    /// <summary>
    /// Schemes
    /// </summary>
    public static class Schemes
    {
        /// <summary>
        /// The name of the federated login scheme
        /// </summary>
        public const string FederatedLogin = "FederatedLogin";
    }

    /// <summary>
    /// Values for the custom LoginTypes claim type
    /// </summary>
    public static class LoginTypes
    {
        /// <summary>
        /// Federation was used for authentication
        /// </summary>
        public const string Federation = "federation";

        /// <summary>
        /// An API key was used for authentication
        /// </summary>
        public const string ApiKey = "api-key";
    }

    /// <summary>
    /// Values for the client ids
    /// </summary>
    public static class ClientIds
    {
        public const string Berg = "berg-client";
    }

    /// <summary>
    /// Endpoint urls
    /// </summary>
    public static class Endpoints
    {
        /// <summary>
        /// Authorization endpoint
        /// </summary>
        public const string BasePath = "/api/openid";

        /// <summary>
        /// Authorization endpoint
        /// </summary>
        public const string Authorization = $"{BasePath}/authorize";

        /// <summary>
        /// Token endpoint
        /// </summary>
        public const string Token = $"{BasePath}/token";

        /// <summary>
        /// Federation callback endpoint
        /// </summary>
        public const string FederationCallback = $"{BasePath}/federation-callback";

        /// <summary>
        /// Introspection endpoint
        /// </summary>
        public const string Introspect = $"{BasePath}/introspect";

        /// <summary>
        /// EndSession endpoint
        /// </summary>
        public const string EndSession = $"{BasePath}/end-session";

        /// <summary>
        /// UserInfo endpoint
        /// </summary>
        public const string UserInfo = $"{BasePath}/user-info";
    }

    /// <summary>
    /// LifeTime constants
    /// </summary>
    public static class Lifetimes
    {
        /// <summary>
        /// The lifetime of the access token
        /// </summary>
        public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(30);

        /// <summary>
        /// The lifetime of the refresh token
        /// </summary>
        public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(1);

        /// <summary>
        /// The lifetime of the federated login cookie
        /// </summary>
        public static readonly TimeSpan FederatedLoginCacheLifetime = TimeSpan.FromDays(5);
    }
}
