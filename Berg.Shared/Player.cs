using System.Text.Json.Serialization;

namespace Berg.Shared;

public class Player
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("teamId")]
    public Guid? TeamId { get; set; }

    [JsonPropertyName("federatedId")]
    public string FederatedId { get; set; } = null!;

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = new();

    [JsonPropertyName("requiredAttributes")]
    public List<PlayerAttribute> RequiredAttributes { get; set; } = new();
}
