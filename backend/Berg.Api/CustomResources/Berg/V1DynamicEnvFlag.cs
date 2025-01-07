using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1DynamicEnvFlag
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
