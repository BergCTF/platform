using System.Text.Json.Serialization;

namespace Berg.Shared;

public class CtfResponse<T>
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("start")]
    public DateTime Start { get; set; }
    
    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
    
    [JsonPropertyName("teams")]
    public bool Teams { get; set; }
}