using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources;

public class V1Challenge : V1BergCustomResource<V1ChallengeSpec>
{
}

public class V1ChallengeSpec
{
    [JsonPropertyName("ctf")]
    public string Ctf { get; set; } = null!;

    [JsonPropertyName("author")]
    public string Author { get; set; } = null!;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;
    
    [JsonPropertyName("flag")]
    public string Flag { get; set; } = null!;

    [JsonPropertyName("hideUntil")]
    public DateTime? HideUntil { get; set; } = null;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = null!;

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();
    
    [JsonPropertyName("containers")]
    public List<V1ChallengeContainer>? Containers { get; set; }
    
    [JsonPropertyName("attachments")]
    public List<V1ChallengeAttachment>? Attachments { get; set; }
}

public class V1ChallengeContainer
{
    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = null!;
    
    [JsonPropertyName("image")]
    public string Image { get; set; } = null!;
    
    [JsonPropertyName("environment")]
    public Dictionary<string, string>? Environment { get; set; }
    
    [JsonPropertyName("ports")]
    public List<V1ChallengePort>? Ports { get; set; }
}

public class V1ChallengePort
{
    [JsonPropertyName("port")]
    public int Port { get; set; } = 80;
    
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "tcp";
    
    [JsonPropertyName("appProtocol")]
    public string AppProtocol { get; set; } = null!;
    
    [JsonPropertyName("type")]
    public V1ChallengePortType Type { get; set; } = V1ChallengePortType.Internal;
}

public enum V1ChallengePortType
{
    Internal,
    PublicPort,
    PublicVHost,
}

public class V1ChallengeAttachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = null!;
    
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = null!;
}