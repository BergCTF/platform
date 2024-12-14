using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1Page : CustomResource<V1PageSpec>
{
    public V1Page() : base(
        "Page",
        "pages",
        "berg.norelect.ch",
        "v1")
    {
    }
}

public class V1PageSpec
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("index")]
    public int Index { get; set; } = 0;

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}