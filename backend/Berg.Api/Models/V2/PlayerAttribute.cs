using System.Text.Json.Serialization;

namespace Berg.Api.Models.V2;

public class PlayerAttribute
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("public")]
    public bool Public { get; set; } = false;

    [JsonPropertyName("required")]
    public bool Required { get; set; } = false;

    [JsonPropertyName("values")]
    public List<PlayerAttributeValue> Values { get; set; } = [];
}

public class PlayerAttributeValue
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}