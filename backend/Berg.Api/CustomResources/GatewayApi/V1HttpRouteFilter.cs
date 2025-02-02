using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public class V1HttpRouteFilter
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("requestRedirect")]
    public V1HTTPRequestRedirectFilter? RequestRedirect { get; set; }
}