using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class Page
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("index")]
    public int Index { get; set; } = 0;

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}