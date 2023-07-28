using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources;

public class V1TraefikIngressRoute : CustomResource<V1TraefikIngressRouteSpec>
{
}

public class V1TraefikIngressRouteSpec
{
    [JsonPropertyName("entryPoints")]
    public List<string>? EntryPoints { get; set; }

    [JsonPropertyName("routes")]
    public List<V1TraefikIngressRouteEntry>? Routes { get; set; }

    [JsonPropertyName("tls")]
    public Dictionary<string, string>? Tls { get; set; }
}

public class V1TraefikIngressRouteEntry
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Rule";
    
    [JsonPropertyName("match")]
    public string? Match { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("services")]
    public List<V1TraefikIngressRouteService>? Services { get; set; }
}

public class V1TraefikIngressRouteService
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("weight")]
    public int? Weight { get; set; }
    
}