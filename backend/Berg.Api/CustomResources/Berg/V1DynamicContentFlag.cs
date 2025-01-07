using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1DynamicContentFlag
{
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    [JsonPropertyName("mode")]
    public int Mode { get; set; } = 292; // octal 444, r--r--r--
}
