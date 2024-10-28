using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public abstract class V1CommonRouteSpec
{
    [JsonPropertyName("parentRefs")]
    public List<V1ParentReference>? ParentRefs { get; set; }
}