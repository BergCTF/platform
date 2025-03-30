using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class Challenge
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("hideUntil")]
    public DateTime? HideUntil { get; set; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "";

    [JsonPropertyName("flagFormat")]
    public string FlagFormat { get; set; } = "";

    [JsonPropertyName("attachments")]
    public List<Attachment> Attachments { get; set; } = [];

    [JsonPropertyName("hasRemote")]
    public bool HasRemote { get; set; }
}

public class Attachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";
}