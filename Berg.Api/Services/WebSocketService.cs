using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Berg.Api.Configuration;
using Berg.Api.CustomResources;
using Berg.Api.Db;
using Berg.Shared;
using Microsoft.AspNetCore.Authorization;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace Berg.Api.Services;

public interface IWebSocketService
{
    Task WebSocketHandler(WebSocket webSocket, Guid? playerId);
    Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter);
    Task PushEventAll<T>(string eventType, T message);
}

public class BergWebSocketConnection
{
    public WebSocket WebSocket { get; set; } = null!;
    public bool IsAuthenticated { get; set; }
    public Guid? PlayerId { get; set; }
}

public class WebSocketService : IWebSocketService
{

    private readonly ILogger<ChallengeService> _logger;
    private readonly CtfConfig _ctfConfig;

    private readonly object _refreshLock = new();
    private List<BergWebSocketConnection> _websockets = new();

    public WebSocketService(
        ILogger<ChallengeService> logger,
        CtfConfig ctfConfig)
    {
        _logger = logger;
        _ctfConfig = ctfConfig;
    }

    public async Task WebSocketHandler(WebSocket webSocket, Guid? playerId)
    {
        _logger.LogInformation("WebSocket connection opened");
        var ws = new BergWebSocketConnection { WebSocket = webSocket, IsAuthenticated = false };

        if (playerId != null)
        {
            ws.IsAuthenticated = true;
            ws.PlayerId = playerId;
        }

        _websockets.Add(ws);
        var buffer = new byte[1024 * 4];
        try
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                // Do nothing with this, websocket is just for pushing events. We just keep this open.
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            _websockets.Remove(ws);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            return;
        }
        _websockets.Remove(ws);
    }

    public async Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter)
    {
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = eventType, message }));
        foreach (var ws in _websockets)
        {
            if (!ws.IsAuthenticated || !filter(ws.PlayerId!.Value)) continue;
            if (ws.WebSocket.State == WebSocketState.Open)
                await ws.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            else if (ws.WebSocket.State == WebSocketState.CloseReceived || ws.WebSocket.State == WebSocketState.CloseSent)
                _websockets.Remove(ws);
        }
    }

    public async Task PushEventAll<T>(string eventType, T message)
    {
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = eventType, message }));
        foreach (var ws in _websockets)
        {
            if (ws.WebSocket.State == WebSocketState.Open)
                await ws.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            else if (ws.WebSocket.State == WebSocketState.CloseReceived || ws.WebSocket.State == WebSocketState.CloseSent)
                _websockets.Remove(ws);
        }
    }
}
