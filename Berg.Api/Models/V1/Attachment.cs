using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class Attachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";
}