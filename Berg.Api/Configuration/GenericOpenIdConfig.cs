using System;

namespace Berg.Api.Configuration;

public class GenericOpenIdConfig
{
    public string? Issuer { get; set; }
    public string? InternalIssuer { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public List<string>? Scopes { get; set; }
    public OpenIdClaimMappings? Claims { get; set; }
}

public class OpenIdClaimMappings {
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
}

