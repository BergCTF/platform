using Berg.Api.Configuration;
using Berg.Api.Models.V2;
using Microsoft.AspNetCore.Mvc;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class MetadataController(CtfConfig ctfConfig) : ControllerBase
{

    [HttpGet]
    [Route("/api/v2/metadata")]
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
            Teams = ctfConfig.Teams,
        };
    }
}