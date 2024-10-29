using System.Security.Claims;
using Berg.Api.Services;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Berg.Api.Controllers;

[ApiController]
public class WebSocketController : ControllerBase
{
    WebSocketService _webSocketService;

    public WebSocketController(
        WebSocketService webSocketService
    )
    {
        _webSocketService = webSocketService;
    }

    [HttpGet]
    [Route("/api/v1/ws")]
    public async Task OpenWebSocketConnection()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            Guid? player = (User.Identity?.IsAuthenticated ?? false) ? Guid.Parse(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!) : null;
            await _webSocketService.WebSocketHandler(webSocket, player);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
