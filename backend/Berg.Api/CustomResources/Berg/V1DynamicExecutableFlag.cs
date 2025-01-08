using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1DynamicExecutableFlag
{
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    [JsonPropertyName("mode")]
    public int Mode { get; set; } = 73; // octal 111, --x--x--x

}