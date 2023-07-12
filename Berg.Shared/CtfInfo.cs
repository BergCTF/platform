using System.Text.Json.Serialization;

namespace Berg.Shared;

public class CtfInfo
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }
    
    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("teams")]
    public bool Teams { get; set; }
}