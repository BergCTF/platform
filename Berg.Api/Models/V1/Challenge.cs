using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class Challenge
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "";

    [JsonPropertyName("flagFormat")]
    public string FlagFormat { get; set; } = "";

    [JsonPropertyName("attachments")]
    public List<Attachment> Attachments { get; set; } = [];

    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("solvedByTeam")]
    public bool SolvedByTeam { get; set; }

    [JsonPropertyName("solvedByPlayer")]
    public bool SolvedByPlayer { get; set; }

    [JsonPropertyName("instantiatable")]
    public bool Instantiatable { get; set; }

    [JsonPropertyName("playerSolves")]
    public List<PlayerSolve> PlayerSolves { get; set; } = [];

    [JsonPropertyName("teamSolves")]
    public List<TeamSolve> TeamSolves { get; set; } = [];
}