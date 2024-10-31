using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class Player
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("teamId")]
    public Guid? TeamId { get; set; }

    [JsonPropertyName("federatedId")]
    public string FederatedId { get; set; } = "";

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = [];

    [JsonPropertyName("requiredAttributes")]
    public List<PlayerAttribute> RequiredAttributes { get; set; } = [];
}
