using Berg.ChallengeServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class WebSocketController : ControllerBase
{
    WebSocketService _webSocketService;
    PlayerService _playerService;

    public WebSocketController(
        WebSocketService webSocketService,
        PlayerService playerService
    )
    {
        _webSocketService = webSocketService;
        _playerService = playerService;
    }

    [HttpGet]
    [Route("/api/v1/ws")]
    public async Task OpenWebSocketConnection()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            var player = (User.Identity?.IsAuthenticated ?? false) ? _playerService.GetPlayer(User) : null;
            await _webSocketService.WebSocketHandler(webSocket, player);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
