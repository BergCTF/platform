using System.Text.Json.Serialization;

namespace Berg.Api.Models;

public class Team
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("players")]
    public List<Guid> Players { get; set; } = [];
}