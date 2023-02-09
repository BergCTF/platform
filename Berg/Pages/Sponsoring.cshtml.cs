using Berg.Configuration;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Berg.Pages;

public class Sponsoring : PageModel
{
    private readonly CtfInfo _ctfInfo;
    
    public List<SponsorInfo> Partners = new();
    public List<SponsorInfo> GoldSponsors = new();
    public List<SponsorInfo> SilverSponsors = new();
    public List<SponsorInfo> BronzeSponsors = new();
    public List<SponsorInfo> PatronSponsors = new();

    public Sponsoring(CtfInfo ctfInfo)
    {
        _ctfInfo = ctfInfo;
    }
    
    public void OnGet()
    {
        Partners = _ctfInfo.Sponsors.Values
            .Where(s => s.Tier == SponsorTier.Partner)
            .OrderBy(s => s.Name)
            .ToList();
        GoldSponsors = _ctfInfo.Sponsors.Values
            .Where(s => s.Tier == SponsorTier.Gold)
            .OrderBy(s => s.Name)
            .ToList();
        SilverSponsors = _ctfInfo.Sponsors.Values
            .Where(s => s.Tier == SponsorTier.Silver)
            .OrderBy(s => s.Name)
            .ToList();
        BronzeSponsors = _ctfInfo.Sponsors.Values
            .Where(s => s.Tier == SponsorTier.Bronze)
            .OrderBy(s => s.Name)
            .ToList();
        PatronSponsors = _ctfInfo.Sponsors.Values
            .Where(s => s.Tier == SponsorTier.Patron)
            .OrderBy(s => s.Name)
            .ToList();
    }
}