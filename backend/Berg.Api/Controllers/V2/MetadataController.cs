using Berg.Api.Configuration;
using Berg.Api.Models.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class MetadataController(CtfConfig ctfConfig) : ControllerBase
{

    [HttpGet]
    [Route("/api/v2/metadata")]
    [Authorize(Policy = Constants.Policies.Anonymous)]
    public Metadata GetMetadata()
    {
        return new Metadata
        {
            Start = ctfConfig.Start,
            End = ctfConfig.End,
            ServerTime = DateTime.UtcNow,
            FreezeStart = ctfConfig.Scoring.FreezeStart,
            FreezeEnd = ctfConfig.Scoring.FreezeEnd,
            AllowAnonymousAccess = ctfConfig.AllowAnonymousAccess,
            PlayerAttributes = ctfConfig.PlayerAttributes?.Select(a => new Models.V2.PlayerAttribute
            {
                Name = a.Name,
                Public = a.Public,
                Required = a.Required,
                Values = a.Values,
            }).ToList() ?? [],
            Teams = ctfConfig.Teams,
        };
    }
}