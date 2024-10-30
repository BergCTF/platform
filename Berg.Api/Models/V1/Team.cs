using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class Team
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("joinToken")]
    public string? JoinToken { get; set; }

    [JsonPropertyName("players")]
    public List<Guid> Players { get; set; } = new();
}