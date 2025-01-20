using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Berg.Api.Services;

public interface IWebSocketService
{
    Task HandleWebSocketConnection(WebSocket webSocket, Guid? playerId, CancellationToken cancellationToken);
    Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter);
    Task PushEventAll<T>(string eventType, T message);
    Task DowngradeExpiredConnections(CancellationToken cancellationToken);
}

public class WebSocketConnection
{
    public Guid Id { get; set; }
    public Guid? PlayerId { get; set; }
    public required WebSocket WebSocket { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required CancellationTokenSource CancellationTokenSource { get; set; }
}

public class WebSocketMessage
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
}

public class WebSocketMessage<T> : WebSocketMessage
{
    [JsonPropertyName("message")]
    public required T Message { get; set; }
}

public class WebSocketService(
    ILogger<ChallengeService> logger,
    BergMetrics metrics,
    IHostApplicationLifetime applicationLifetime) : IWebSocketService
{
    private readonly List<WebSocketConnection> _connections = [];

    public async Task HandleWebSocketConnection(WebSocket webSocket, Guid? playerId, CancellationToken cancellationToken)
    { 
        var connection = new WebSocketConnection
        {
            Id = Guid.NewGuid(),
            WebSocket = webSocket,
            PlayerId = playerId,
            CreatedAt = DateTime.UtcNow,
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                applicationLifetime.ApplicationStopping,
                cancellationToken)
        };
        logger.LogDebug("WebSocket connection {Id} opened for player: {PlayerId}", connection.Id, playerId);
        metrics.WebSocketStarted(playerId ?? Guid.Empty);

        _connections.Add(connection);

        await SendPlayerIdMessage(connection);

        var buffer = new byte[1024 * 4];
        while (!connection.CancellationTokenSource.IsCancellationRequested)
        {
            var webSocketMessage = await ReceiveMessage<int>(connection, buffer);
            if (webSocketMessage != null && webSocketMessage.Type == "ping" &&
                !connection.CancellationTokenSource.IsCancellationRequested) {
                var messageBytes = SerializeMessage("pong", webSocketMessage.Message);
                await SendMessage(connection, messageBytes);
            }
        }
        await CloseConnection(connection);
    }

    public async Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        var messageBytes = SerializeMessage(eventType, message);
        var sendTasks = _connections
            .Where(c => c.PlayerId.HasValue && filter(c.PlayerId.Value))
            .Select(c => SendMessage(c, messageBytes))
            .ToArray();
        await Task.WhenAll(sendTasks);
    }

    public async Task PushEventAll<T>(string eventType, T message)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        var messageBytes = SerializeMessage(eventType, message);
        var sendTasks = _connections
            .Select(c => SendMessage(c, messageBytes))
            .ToArray();
        await Task.WhenAll(sendTasks);
    }

    public async Task DowngradeExpiredConnections(CancellationToken cancellationToken)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        foreach (var conn in _connections.ToList())
        {
            // Skip unauthenticated connections
            if (conn == null || !conn.PlayerId.HasValue)
                continue;

            // Only look at expired connections
            if (DateTime.UtcNow < conn.CreatedAt.Add(Constants.Lifetimes.AccessTokenLifetime))
                continue;

            // Downgrade socket by removing player association, keep socket alive to still receive public events
            conn.PlayerId = null;
            await SendPlayerIdMessage(conn);
            logger.LogDebug("WebSocket connection {} was unauthenticated", conn.Id);
        }
    }

    private async Task SendPlayerIdMessage(WebSocketConnection connection)
    {
        var messageBytes = SerializeMessage("current-player", connection.PlayerId?.ToString());
        await SendMessage(connection, messageBytes);
    }

    private async Task SendMessage(WebSocketConnection connection, byte[] messageBytes)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        try
        {
            await connection.WebSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "WebSocket connection {} had an exception calling SendMessage<T>()", connection.Id);
            await CloseConnection(connection);
        }
    }

    private async Task<WebSocketMessage<T>?> ReceiveMessage<T>(WebSocketConnection connection, byte[] buffer)
    {
        var segment = new ArraySegment<byte>(buffer);
        var cancellationToken = connection.CancellationTokenSource.Token;
        try
        {
            // If the client wants to close the connection, close it.
            if (connection.WebSocket.State == WebSocketState.CloseReceived)
            {
                await CloseConnection(connection);
                return null;
            }

            var result = await connection.WebSocket.ReceiveAsync(segment, cancellationToken);

            // Ignore empty messages
            if (result.Count == 0)
                return null;

            // If the client wants to close the connection, close it.
            if (result.CloseStatus.HasValue)
            {
                await CloseConnection(connection);
                return null;
            }

            var receivedBytes = segment[0..result.Count];
            try
            {
                return JsonSerializer.Deserialize<WebSocketMessage<T>>(receivedBytes);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "WebSocket connection {Id} got invalid json from player: {PlayerId} {HexData}", connection.Id, connection.PlayerId, Convert.ToHexString(receivedBytes));
            }
        }
        catch (WebSocketException ex)
        {
            logger.LogError(ex, "WebSocket connection {} had an exception calling ReceiveMessage<T>()", connection.Id);
            await CloseConnection(connection);
        }
        return null;
    }

    private async Task CloseConnection(WebSocketConnection connection)
    {
        if(_connections.Remove(connection))
        {
            logger.LogDebug("WebSocket connection {} closed", connection.Id);
            metrics.WebSocketStopped(connection.PlayerId ?? Guid.Empty);
        }
        connection.CancellationTokenSource.Cancel();
        try
        {
            await connection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
        } catch {}
    }

    private static byte[] SerializeMessage<T>(string type, T message)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        return JsonSerializer.SerializeToUtf8Bytes(new WebSocketMessage<T>
        {
            Type = type,
            Message = message,
        });
    }
}
