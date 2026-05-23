using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public class V1TLSRoute : CustomResource<V1TLSRouteSpec>
{
    public V1TLSRoute() : base(
        "TLSRoute",
        "tlsroutes",
        "gateway.networking.k8s.io",
        "v1")
    {
    }
}

public class V1TLSRouteSpec : V1CommonRouteSpec
{
    [JsonPropertyName("hostnames")]
    public List<string>? Hostnames { get; set; }

    [JsonPropertyName("rules")]
    public List<V1TLSRouteRule>? Rules { get; set; }
}
