using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources.GatewayApi;

public class V1Alpha2TLSRouteRule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("backendRefs")]
    public List<V1BackendRef>? BackendRefs { get; set; }
}