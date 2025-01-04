using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Berg.Api.Configuration;

public class GenericOpenIdConfig
{
    public string? Issuer { get; set; }
    public string? InternalIssuer { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public List<string>? Scopes { get; set; }
    public OpenIdClaimMappings Claims { get; set; } = new();
    public OpenIdRoleMappings Roles { get; set; } = new();
}

public class OpenIdClaimMappings {
    public string Id { get; set; } = Claims.Subject;
    public string Name { get; set; } = Claims.Name;
    public string Email { get; set; } = Claims.Email;
    public string Role { get; set; } = Claims.Role;
}

public class OpenIdRoleMappings {
    public string Player { get; set; } = Constants.Roles.Player;
    public string Author { get; set; } = Constants.Roles.Author;
    public string Admin { get; set; } = Constants.Roles.Admin;
}
