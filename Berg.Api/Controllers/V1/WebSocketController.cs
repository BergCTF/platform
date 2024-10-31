using System.Security.Claims;
using Berg.Api.Services;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Berg.Api.Controllers.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
public class WebSocketController(WebSocketService webSocketService) : ControllerBase
{

    [HttpGet]
    [Route("/api/v1/ws")]
    public async Task OpenWebSocketConnection()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            Guid? player = (User.Identity?.IsAuthenticated ?? false) ? Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!) : null;
            await webSocketService.WebSocketHandler(webSocket, player);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
