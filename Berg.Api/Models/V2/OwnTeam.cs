using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class OwnTeam
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("joinToken")]
    public string JoinToken { get; set; } = "";

    [JsonPropertyName("players")]
    public List<Guid> Players { get; set; } = [];
}