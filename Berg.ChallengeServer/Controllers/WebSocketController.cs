using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Challenge = Berg.Shared.Challenge;

namespace Berg.ChallengeServer.Controllers;

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
            await _webSocketService.WebSocketHandler(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

}
