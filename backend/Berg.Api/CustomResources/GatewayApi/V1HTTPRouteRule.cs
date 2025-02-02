using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public class V1HTTPRouteRule
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("filters")]
    public List<V1HttpRouteFilter>? Filters { get; set; }

    [JsonPropertyName("backendRefs")]
    public List<V1HttpBackendRef>? BackendRefs { get; set; }
}