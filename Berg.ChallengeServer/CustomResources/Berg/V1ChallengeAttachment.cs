using System.Text.Json.Serialization;

namespace Berg.ChallengeServer.CustomResources.Berg;

public class V1ChallengeAttachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = null!;
    
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = null!;
}