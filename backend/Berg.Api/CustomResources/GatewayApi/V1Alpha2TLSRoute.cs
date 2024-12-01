using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public class V1Alpha2TLSRoute : CustomResource<V1Alpha2TLSRouteSpec>
{
    public V1Alpha2TLSRoute() : base(
        "TLSRoute",
        "tlsroutes",
        "gateway.networking.k8s.io",
        "v1alpha2")
    {
    }
}

public class V1Alpha2TLSRouteSpec : V1CommonRouteSpec
{
    [JsonPropertyName("hostnames")]
    public List<string>? Hostnames { get; set; }

    [JsonPropertyName("rules")]
    public List<V1Alpha2TLSRouteRule>? Rules { get; set; }
}