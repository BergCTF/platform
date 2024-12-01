using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Berg.Api.Services;

public interface IWebSocketService
{
    Task WebSocketHandler(WebSocket webSocket, Guid? playerId);
    Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter);
    Task PushEvent<T>(string eventType, T message);
}

public class BergWebSocketConnection
{
    public WebSocket WebSocket { get; set; } = null!;
    public bool IsAuthenticated { get; set; }
    public Guid? PlayerId { get; set; }
}

public class WebSocketService(ILogger<ChallengeService> logger) : IWebSocketService
{
    private readonly List<BergWebSocketConnection> _connections = [];

    public async Task WebSocketHandler(WebSocket webSocket, Guid? playerId)
    {
        logger.LogInformation("WebSocket connection opened");
        var conn = new BergWebSocketConnection { WebSocket = webSocket, IsAuthenticated = false };

        if (playerId != null)
        {
            conn.IsAuthenticated = true;
            conn.PlayerId = playerId;
        }

        _connections.Add(conn);
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
            _connections.Remove(conn);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            return;
        }
        _connections.Remove(conn);
    }

    public async Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter)
    {
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = eventType, message }));
        foreach (var conn in _connections)
        {
            if (!conn.IsAuthenticated || !filter(conn.PlayerId!.Value)) continue;
            if (conn.WebSocket.State == WebSocketState.Open)
                await conn.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            else if (conn.WebSocket.State == WebSocketState.CloseReceived || conn.WebSocket.State == WebSocketState.CloseSent)
                _connections.Remove(conn);
        }
    }

    public Task PushEvent<T>(string eventType, T message)
    {
        return PushEvent(eventType, message, _ => true);
    }
}
