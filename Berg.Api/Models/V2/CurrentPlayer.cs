using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class CurrentPlayer
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = [];

    [JsonPropertyName("teamId")]
    public Guid? TeamId { get; set; }

    [JsonPropertyName("federatedId")]
    public string FederatedId { get; set; } = "";

    [JsonPropertyName("apiKeyPlaceholder")]
    public string? ApiKeyPlaceholder { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = [];
}
