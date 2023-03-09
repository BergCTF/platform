namespace Berg.Configuration;

public class SponsorInfo
{
    public string Name { get; set; }
    public string Description { get; set; }
    public SponsorTier Tier { get; set; }
    public string Logo { get; set; }
    public string Website { get; set; }
    public string Background { get; set; } = "dark";
    public bool HideOnSponsoringPage { get; set; }
}