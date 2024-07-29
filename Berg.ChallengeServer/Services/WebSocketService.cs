using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Db;
using Berg.Shared;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace Berg.ChallengeServer.Services;

public interface IWebSocketService
{
    Task WebSocketHandler(WebSocket webSocket);
    Task PushEvent<T>(string eventType, T message);
}

public class WebSocketService : IWebSocketService
{

    private readonly ILogger<ChallengeService> _logger;
    private readonly CtfConfig _ctfConfig;

    private readonly object _refreshLock = new();
    private List<WebSocket> _websockets = new();

    public WebSocketService(
        ILogger<ChallengeService> logger,
        CtfConfig ctfConfig)
    {
        _logger = logger;
        _ctfConfig = ctfConfig;
    }

    public async Task WebSocketHandler(WebSocket webSocket)
    {
        _websockets.Add(webSocket);
        var buffer = new byte[1024 * 4];
        WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        while (!result.CloseStatus.HasValue)
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        _websockets.Remove(webSocket);
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }

    public async Task PushEvent<T>(string eventType, T message)
    {
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = eventType, message }));
        foreach (var ws in _websockets)
        {
            if (ws.State == WebSocketState.Open)
                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            else if (ws.State == WebSocketState.CloseReceived || ws.State == WebSocketState.CloseSent)
                _websockets.Remove(ws);
        }
    }

}
