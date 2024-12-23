using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Berg.Api.Services;

public interface IWebSocketService
{
    Task WebSocketHandler(WebSocket webSocket, Guid? playerId, CancellationToken cancellationToken);
    Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter);
    Task PushEventAll<T>(string eventType, T message);
    Task DowngradeExpiredConnections(CancellationToken cancellationToken);
}

public class BergWebSocketConnection
{
    public WebSocket WebSocket { get; set; } = null!;
    public Guid? PlayerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.MinValue;
}

public class WebSocketService(ILogger<ChallengeService> logger) : IWebSocketService
{
    private readonly List<BergWebSocketConnection> _connections = [];

    public async Task WebSocketHandler(WebSocket webSocket, Guid? playerId, CancellationToken cancellationToken)
    {
        logger.LogDebug("WebSocket connection opened for player: {PlayerId}", playerId);
        var conn = new BergWebSocketConnection
        {
            WebSocket = webSocket,
            PlayerId = playerId,
            CreatedAt = DateTime.UtcNow,
        };

        _connections.Add(conn);
        await SendPlayerIdMessage(conn.WebSocket, playerId, cancellationToken);

        var buffer = new byte[1024 * 4];
        try
        {
            WebSocketReceiveResult result;
            do
            {
                // Do nothing with this, websocket is just for pushing events. We just keep this open.
                var segment = new ArraySegment<byte>(buffer);
                result = await webSocket.ReceiveAsync(segment, cancellationToken);
            } while (!result.CloseStatus.HasValue);
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, cancellationToken);
        }
        catch (WebSocketException)
        {
            _connections.Remove(conn);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", cancellationToken);
            return;
        }
        _connections.Remove(conn);
        logger.LogDebug("WebSocket connection closed for player: {PlayerId}", playerId);
    }

    public async Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter)
    {
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = eventType, message }));
        foreach (var conn in _connections)
        {
            if (!conn.PlayerId.HasValue || !filter(conn.PlayerId.Value))
                continue;

            if (conn.WebSocket.State == WebSocketState.Open)
            {
                await conn.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else if (conn.WebSocket.State == WebSocketState.CloseReceived || conn.WebSocket.State == WebSocketState.CloseSent)
            {
                _connections.Remove(conn);
            }
        }
    }

    public async Task PushEventAll<T>(string eventType, T message)
    {
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = eventType, message }));
        foreach (var conn in _connections)
        {
            if (conn.WebSocket.State == WebSocketState.Open)
            {
                await conn.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else if (conn.WebSocket.State == WebSocketState.CloseReceived || conn.WebSocket.State == WebSocketState.CloseSent)
            {
                _connections.Remove(conn);
            }
        }
    }

    public async Task DowngradeExpiredConnections(CancellationToken cancellationToken)
    {
        foreach (var conn in _connections.ToList())
        {
            // Skip unauthenticated connections
            if (!conn.PlayerId.HasValue)
                continue;
            // Only look at expired connections
            if (DateTime.UtcNow < conn.CreatedAt.Add(Constants.Lifetimes.AccessTokenLifetime))
                continue;

            // Downgrade socket by removing player association, keep socket alive to still receive public events
            conn.PlayerId = null;
            await SendPlayerIdMessage(conn.WebSocket, null, cancellationToken);
            logger.LogDebug("WebSocket connection downgraded for player: {PlayerId}", conn.PlayerId);
        }
    }

    private static async Task SendPlayerIdMessage(WebSocket webSocket, Guid? playerId, CancellationToken cancellationToken)
    {
        string? playerIdStr = playerId?.ToString();
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "current-player", message = playerIdStr }));
        await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);
    }
}
