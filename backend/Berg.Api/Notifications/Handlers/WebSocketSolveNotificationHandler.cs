using Berg.Api.Configuration;
using Berg.Api.Controllers.V2;
using Berg.Api.Db;
using Berg.Api.Services;
using MediatR;

namespace Berg.Api.Notifications.Handlers;

public class WebSocketSolveNotificationHandler(WebSocketService webSocketService,
    CtfConfig ctfConfig,
    BergDbContext dbContext,
    ILogger<WebSocketSolveNotificationHandler> logger) :
    INotificationHandler<SolveNotification>,
    INotificationHandler<PlayerCreateNotification>,
    INotificationHandler<PlayerUpdateNotification>,
    INotificationHandler<TeamCreateNotification>,
    INotificationHandler<TeamUpdateNotification>
{
    public async Task Handle(SolveNotification solve, CancellationToken cancellationToken)
    {
        var dtoSolve = new Models.V2.Solve
        {
            Id = solve.Id,
            PlayerId = solve.PlayerId,
            ChallengeName = solve.Challenge,
            SolvedAt = solve.SolvedAt,
            TeamId = solve.TeamId
        };
        if (!solve.IsFrozen)
        {
            logger.LogDebug("Messaging all players about this solve.");
            await webSocketService.PushEvent("solve", dtoSolve);
        }
        else if (ctfConfig.Teams)
        {
            logger.LogDebug("Only messaging specific team players due to freeze.");
            var teamPlayerIds = dbContext.Players.Where(p => p.TeamId == solve.TeamId).Select(p => p.Id).ToHashSet();
            await webSocketService.PushEvent("solve", dtoSolve, teamPlayerIds.Contains);
        }
        else
        {
            logger.LogDebug("Only messaging specific player due to freeze.");
            await webSocketService.PushEvent("solve", dtoSolve, p => solve.PlayerId == p);
        }
    }

    public async Task Handle(PlayerCreateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending player created message to websocket clients.");
        var publicCustomAttributeNames = PlayerController.GetPublicCustomAttributeNames(ctfConfig);
        var player = PlayerController.ToModelPlayer(notification.DbPlayer, publicCustomAttributeNames);
        await webSocketService.PushEvent("player", player);
    }

    public async Task Handle(PlayerUpdateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending player updated message to websocket clients.");
        var publicCustomAttributeNames = PlayerController.GetPublicCustomAttributeNames(ctfConfig);
        var player = PlayerController.ToModelPlayer(notification.DbPlayer, publicCustomAttributeNames);
        await webSocketService.PushEvent("player", player);
    }

    public async Task Handle(TeamCreateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending team created message to websocket clients.");
        await webSocketService.PushEvent("team", notification);
    }

    public async Task Handle(TeamUpdateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending team updated message to websocket clients.");
        await webSocketService.PushEvent("team", notification);
    }
}
