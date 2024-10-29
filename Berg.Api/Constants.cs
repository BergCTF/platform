namespace Berg.Api;

public static class Constants
{
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
        public const string Authorization = "/api/v1/authorize";

        /// <summary>
        /// Token endpoint
        /// </summary>
        public const string Token = "/api/v1/token";

        /// <summary>
        /// Federation callback endpoint
        /// </summary>
        public const string FederationCallback = "/api/v1/federation-callback";

        /// <summary>
        /// Introspection endpoint
        /// </summary>
        public const string Introspect = "/api/v1/introspect";

        /// <summary>
        /// Logout endpoint
        /// </summary>
        public const string Logout = "/api/v1/logout";
    }
}
