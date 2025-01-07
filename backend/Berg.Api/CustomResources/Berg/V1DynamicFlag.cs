using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1DynamicFlag
{
    [JsonPropertyName("env")]
    public V1DynamicEnvFlag? Env { get; set; } = null;

    [JsonPropertyName("content")]
    public V1DynamicContentFlag? Content { get; set; } = null!;

    [JsonPropertyName("executable")]
    public V1DynamicExecutableFlag? Executable { get; set; } = null!;
}
