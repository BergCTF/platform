using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.GatewayApi;

public class V1HTTPRequestRedirectFilter
{
    [JsonPropertyName("scheme")]
    public string? Scheme { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }
}