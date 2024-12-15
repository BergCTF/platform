using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class Player
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = [];
}
