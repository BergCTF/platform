namespace Berg.Api.Models.V1;

public class PlayerAttribute
{
    public string Name { get; set; } = "";
    public bool Public { get; set; } = false;
    public bool Required { get; set; } = false;
    public List<string> Values { get; set; } = [];
}