using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1ChallengeAttachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = null!;

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; } = null;

    [JsonPropertyName("downloadImage")]
    public string? DownloadImage { get; set; } = null;

    [JsonPropertyName("downloadImagePullSecret")]
    public string? DownloadImagePullSecret { get; set; } = null;

    [JsonPropertyName("downloadImageInsecure")]
    public bool DownloadImageInsecure { get; set; } = false;
}