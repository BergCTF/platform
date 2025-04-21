using System.Security.Claims;
using Berg.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Berg.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName="berg-api")]
public class EventsController(IWebSocketService webSocketService) : ControllerBase
{
    [HttpGet]
    [Route("/api/v2/events")]
    [Authorize(Policy = Constants.Policies.Anonymous)]
    [ProducesResponseType(StatusCodes.Status101SwitchingProtocols)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> OpenWebSocketConnection(CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad request",
                Detail = "The received request is not a web socket request"
            });
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await webSocketService.HandleWebSocketConnection(webSocket, cancellationToken);
        return new EmptyResult();
    }
}
