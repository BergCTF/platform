using System.Text.Json.Serialization;

namespace Berg.Shared;

public class Attachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; }
    
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; }
}