using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources.GatewayApi;

public class V1BackendRef : BackendObjectReference
{
    [JsonPropertyName("weight")]
    public int? Weight { get; set; }
}