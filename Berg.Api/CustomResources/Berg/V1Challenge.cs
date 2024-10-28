using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1Challenge : CustomResource<V1ChallengeSpec>
{
    public V1Challenge() : base(
        "Challenge",
        "challenges",
        "berg.norelect.ch",
        "v1")
    {
    }
}

public class V1ChallengeSpec
{
    [JsonPropertyName("author")]
    public string Author { get; set; } = null!;

    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonPropertyName("flag")]
    public string Flag { get; set; } = null!;

    [JsonPropertyName("flagFormat")]
    public string FlagFormat { get; set; } = "flag{...}";

    [JsonPropertyName("hideUntil")]
    public DateTime? HideUntil { get; set; } = null;

    [JsonPropertyName("staticValue")]
    public int? StaticValue { get; set; } = null;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = null!;

    [JsonPropertyName("allowOutboundTraffic")]
    public bool AllowOutboundTraffic { get; set; } = false;

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("containers")]
    public List<V1ChallengeContainer>? Containers { get; set; }

    [JsonPropertyName("attachments")]
    public List<V1ChallengeAttachment>? Attachments { get; set; }
}