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
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; } = null;

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("flag")]
    public string Flag { get; set; } = "";

    [JsonPropertyName("flagFormat")]
    public string FlagFormat { get; set; } = "flag{...}";

    [JsonPropertyName("dynamicFlagMode")]
    public V1DynamicFlagMode DynamicFlagMode { get; set; } = V1DynamicFlagMode.Suffix;

    [JsonPropertyName("hideUntil")]
    public DateTime? HideUntil { get; set; }

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "";

    [JsonPropertyName("allowOutboundTraffic")]
    public bool AllowOutboundTraffic { get; set; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("event")]
    public string? Event { get; set; }

    [JsonPropertyName("containers")]
    public List<V1ChallengeContainer>? Containers { get; set; }

    [JsonPropertyName("attachments")]
    public List<V1ChallengeAttachment>? Attachments { get; set; }

    [JsonIgnore]
    public bool SupportsDynamicFlags => Containers?.Any(c => c.DynamicFlag != null) ?? false;
}
