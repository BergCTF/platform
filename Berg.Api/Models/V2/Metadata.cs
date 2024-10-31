using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class Metadata
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("serverTime")]
    public DateTime ServerTime { get; set; }

    [JsonPropertyName("freezeStart")]
    public DateTime? FreezeStart { get; set; }

    [JsonPropertyName("freezeEnd")]
    public DateTime? FreezeEnd { get; set; }

    [JsonPropertyName("teams")]
    public bool Teams { get; set; }
}