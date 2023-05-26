using System.Text.Json.Serialization;

namespace Berg.Shared;

public class Ctf
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("start")]
    public DateTime Start { get; set; }
    
    [JsonPropertyName("end")]
    public DateTime End { get; set; }
}