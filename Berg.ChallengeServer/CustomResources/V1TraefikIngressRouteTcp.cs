using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources;

public class V1TraefikIngressRouteTcp : CustomResource<V1TraefikIngressRouteTcpSpec>
{
}

public class V1TraefikIngressRouteTcpSpec
{
    [JsonPropertyName("entryPoints")]
    public List<string>? EntryPoints { get; set; }

    [JsonPropertyName("routes")]
    public List<V1TraefikRoute>? Routes { get; set; }

    [JsonPropertyName("tls")]
    public Dictionary<string, string>? Tls { get; set; }
}

public class V1TraefikRoute
{
    [JsonPropertyName("match")]
    public string? Match { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("services")]
    public List<V1TraefikService>? Services { get; set; }
}

public class V1TraefikService
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("weight")]
    public int? Weight { get; set; }

    [JsonPropertyName("terminationDelay")]
    public int? TerminationDelay { get; set; }
}