using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public class V1ParentReference
{
    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("sectionName")]
    public string? SectionName { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }
}