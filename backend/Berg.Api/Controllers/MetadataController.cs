using Berg.Api.Configuration;
using Berg.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName="berg-api")]
public class MetadataController(CtfConfig ctfConfig) : ControllerBase
{

    [HttpGet]
    [Route("/api/metadata")]
    [Authorize(Policy = Constants.Policies.AnonymousIfAllowedOrPlayer)]
    public Metadata GetMetadata()
    {
        return new Metadata
        {
            Version = Environment.GetEnvironmentVariable("BERG_VERSION") ?? "0.0.0",
            EventName = ctfConfig.EventName,
            EventOrganiser = ctfConfig.EventOrganiser,
            EventLogoUrl = ctfConfig.EventLogoUrl,
            Start = ctfConfig.Start,
            End = ctfConfig.End,
            FreezeStart = ctfConfig.Scoring.FreezeStart,
            FreezeEnd = ctfConfig.Scoring.FreezeEnd,
            AllowAnonymousAccess = ctfConfig.AllowAnonymousAccess,
            PlayerAttributes = ctfConfig.PlayerAttributes?.Select(a => new Models.PlayerAttribute
            {
                Name = a.Name,
                Title = a.Title,
                Description = a.Description,
                Public = a.Public,
                Required = a.Required,
                Values = a.Values?.Select(v => new Models.PlayerAttributeValue
                {
                    Value = v.Value,
                    Title = v.Title,
                    Description = v.Description
                }).ToList() ?? [],
            }).ToList() ?? [],
            Teams = ctfConfig.Teams,
            ChallengeMaximumValue = ctfConfig.Scoring.MaximumScore,
            ChallengeMinimumValue = ctfConfig.Scoring.MinimumScore,
            ChallengeSolvesBeforeMinimum = ctfConfig.Scoring.NumSolvesBeforeMinimum,
        };
    }
}