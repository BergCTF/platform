using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public class V1HTTPRoute : CustomResource<V1HTTPRouteSpec>
{
    public V1HTTPRoute() : base(
        "HTTPRoute",
        "httproutes",
        "gateway.networking.k8s.io",
        "v1")
    {
    }
}

public class V1HTTPRouteSpec : V1CommonRouteSpec
{
    [JsonPropertyName("hostnames")]
    public List<string>? Hostnames { get; set; }

    [JsonPropertyName("rules")]
    public List<V1HTTPRouteRule>? Rules { get; set; }
}