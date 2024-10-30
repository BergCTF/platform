using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class Challenge
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("author")]
    public string Author { get; set; } = null!;

    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = null!;

    [JsonPropertyName("flagFormat")]
    public string FlagFormat { get; set; } = null!;

    [JsonPropertyName("attachments")]
    public List<Attachment> Attachments { get; set; } = new();

    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("solvedByTeam")]
    public bool SolvedByTeam { get; set; }

    [JsonPropertyName("solvedByPlayer")]
    public bool SolvedByPlayer { get; set; }

    [JsonPropertyName("instantiable")]
    public bool Instantiable { get; set; }
}

public class Attachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = default!;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = default!;
}