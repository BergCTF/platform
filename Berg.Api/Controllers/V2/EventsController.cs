using System.Security.Claims;
using Berg.Api.Configuration;
using Berg.Api.Services;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Berg.Api.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
public class EventsController(WebSocketService webSocketService, CtfConfig ctfConfig) : ControllerBase
{

    [HttpGet]
    [Route("/api/v2/events")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> OpenWebSocketConnection()
    {
        if (!ctfConfig.AllowAnonymousAccess &&
            !(HttpContext.User.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }

        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            Guid? player = (User.Identity?.IsAuthenticated ?? false) ? Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!) : null;
            await webSocketService.WebSocketHandler(webSocket, player);
        }
        else
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad request",
                Detail = "The received request is not a web socket request"
            });
        }
        return Ok();
    }
}
