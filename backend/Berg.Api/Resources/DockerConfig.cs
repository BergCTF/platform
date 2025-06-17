using System.Text.Json.Serialization;

namespace Berg.Api.Resources;

public class DockerConfig
{
    [JsonPropertyName("auths")]
    public Dictionary<string, DockerAuth>? Authentications  { get; set; }
}

public class DockerAuth
{
    [JsonPropertyName("auth")]
    public string? Authentication { get; set; }
}