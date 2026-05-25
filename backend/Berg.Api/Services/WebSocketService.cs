using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Berg.Api.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Validation;

namespace Berg.Api.Services;

public interface IWebSocketService
{
    Task HandleWebSocketConnection(WebSocket webSocket, CancellationToken cancellationToken);
    Task PushEvent<T>(string eventType, T message, Func<Guid, bool> filter);
    Task PushEventAll<T>(string eventType, T message);
    Task DowngradeExpiredConnections(CancellationToken cancellationToken);
}

public class WebSocketConnection
{
    public Guid Id { get; set; }
    public Guid? PlayerId { get; set; }
    public required WebSocket WebSocket { get; set; }
    public required DateTime? ExpiresAt { get; set; }
    public readonly SemaphoreSlim SendMessageSemaphore = new(1, 1);
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
    OpenIddictValidationService openIddictValidationService,
    CtfConfig ctfConfig,
    IHostApplicationLifetime applicationLifetime) : IWebSocketService
{
    private readonly List<WebSocketConnection> _connections = [];
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

    public async Task HandleWebSocketConnection(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var connection = new WebSocketConnection
        {
            Id = Guid.NewGuid(),
            WebSocket = webSocket,
            PlayerId = null,
            ExpiresAt = null,
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                applicationLifetime.ApplicationStopping,
                cancellationToken)
        };
        logger.LogDebug("WebSocket connection {ConnectionId} opened", connection.Id);
        metrics.WebSocketStarted();

        await _connectionSemaphore.WaitAsync(cancellationToken);
        try
        {
            _connections.Add(connection);
        }
        finally
        {
            _connectionSemaphore.Release();
        }

        var buffer = new byte[1024 * 4];
        while (!connection.CancellationTokenSource.IsCancellationRequested)
        {
            var webSocketMessage = await ReceiveMessage<JsonElement?>(connection, buffer);
            if (webSocketMessage != null)
            {
                if (webSocketMessage.Type == "ping" &&
                    !connection.CancellationTokenSource.IsCancellationRequested)
                {
                    var messageBytes = SerializeMessage("pong", webSocketMessage.Message);
                    await SendMessage(connection, messageBytes);
                }
                else if (webSocketMessage.Type == "auth" &&
                    !connection.CancellationTokenSource.IsCancellationRequested)
                {
                    var token = webSocketMessage.Message?.Deserialize<string?>();
                    if (string.IsNullOrEmpty(token))
                    {
                        connection.PlayerId = null;
                        logger.LogDebug("WebSocket connection {ConnectionId} got empty token during auth call", connection.Id);
                        await SendPlayerIdMessage(connection);
                    }
                    else
                    {
                        try
                        {
                            var principal = await openIddictValidationService.ValidateAccessTokenAsync(token ?? "", connection.CancellationTokenSource.Token);
                            connection.PlayerId = Guid.Parse(principal.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
                            connection.ExpiresAt = principal.GetExpirationDate()?.UtcDateTime;
                            logger.LogDebug("WebSocket connection {ConnectionId} was authenticated", connection.Id);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "WebSocket connection {ConnectionId} got exception during authentication", connection.Id);
                            connection.PlayerId = null;
                        }
                        // Report back the current authentication state
                        await SendPlayerIdMessage(connection);
                    }
                }
                else
                {
                    var messageBytes = SerializeMessage("error", "Invalid message type");
                    logger.LogDebug("WebSocket connection {ConnectionId} got invalid message type: {WebSocketMessageType}", connection.Id, webSocketMessage.Type);
                    await SendMessage(connection, messageBytes);
                }
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
            .Where(c => ctfConfig.AllowAnonymousAccess || c.PlayerId.HasValue)
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

            // Skip connections that have not expired yet
            if (DateTime.UtcNow < conn.ExpiresAt)
                continue;

            // Downgrade socket by removing player association, keep socket alive to still receive public events
            conn.PlayerId = null;
            conn.ExpiresAt = null;
            await SendPlayerIdMessage(conn);
            logger.LogDebug("WebSocket connection {ConnectionId} was unauthenticated", conn.Id);
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

        await connection.SendMessageSemaphore.WaitAsync().ConfigureAwait(false); ;
        try
        {
            await connection.WebSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "WebSocket connection {ConnectionId} had an exception calling SendMessage<T>()", connection.Id);
            await CloseConnection(connection);
        }
        finally
        {
            connection.SendMessageSemaphore.Release();
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
                logger.LogWarning(ex, "WebSocket connection {ConnectionId} got invalid json from player: {PlayerId} {HexData}", connection.Id, connection.PlayerId, Convert.ToHexString(receivedBytes));
            }
        }
        catch (WebSocketException ex)
        {
            logger.LogError(ex, "WebSocket connection {ConnectionId} had an exception calling ReceiveMessage<T>()", connection.Id);
            await CloseConnection(connection);
        }
        return null;
    }

    private async Task CloseConnection(WebSocketConnection connection)
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (_connections.Remove(connection))
            {
                logger.LogDebug("WebSocket connection {ConnectionId} closed", connection.Id);
                metrics.WebSocketStopped();
            }
        }
        finally
        {
            _connectionSemaphore.Release();
        }
        connection.CancellationTokenSource.Cancel();
        try
        {
            await connection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
        }
        catch { }
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
