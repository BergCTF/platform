using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public class V1TLSRouteRule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("backendRefs")]
    public List<V1BackendRef>? BackendRefs { get; set; }
}
