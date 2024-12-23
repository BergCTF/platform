using Berg.Api.Configuration;
using Berg.Api.Controllers.V2;
using Berg.Api.Db;
using Berg.Api.Services;
using MediatR;

namespace Berg.Api.Notifications.Handlers;

public class WebSocketNotificationHandler(
    IWebSocketService webSocketService,
    CtfConfig ctfConfig,
    BergDbContext dbContext,
    ILogger<WebSocketNotificationHandler> logger) :
    INotificationHandler<SolveNotification>,
    INotificationHandler<PlayerCreateNotification>,
    INotificationHandler<PlayerUpdateNotification>,
    INotificationHandler<TeamCreateNotification>,
    INotificationHandler<TeamUpdateNotification>,
    INotificationHandler<PageCreateNotification>,
    INotificationHandler<PageUpdateNotification>,
    INotificationHandler<ChallengeCreateNotification>,
    INotificationHandler<ChallengeUpdateNotification>
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
            await webSocketService.PushEventAll("solve", dtoSolve);
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
        await webSocketService.PushEventAll("player", player);
    }

    public async Task Handle(PlayerUpdateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending player updated message to websocket clients.");
        var publicCustomAttributeNames = PlayerController.GetPublicCustomAttributeNames(ctfConfig);
        var player = PlayerController.ToModelPlayer(notification.DbPlayer, publicCustomAttributeNames);
        await webSocketService.PushEventAll("player", player);
    }

    public async Task Handle(TeamCreateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending team created message to websocket clients.");
        await webSocketService.PushEventAll("team", notification.Team);
    }

    public async Task Handle(TeamUpdateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending team updated message to websocket clients.");
        await webSocketService.PushEventAll("team", notification.Team);
    }

    public async Task Handle(PageCreateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending page created message to websocket clients.");
        var dtoPage = PageController.ToPage(notification.Page);
        await webSocketService.PushEventAll("page", dtoPage);
    }

    public async Task Handle(PageUpdateNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending page updated message to websocket clients.");
        var dtoPage = PageController.ToPage(notification.Page);
        await webSocketService.PushEventAll("page", dtoPage);
    }

    public async Task Handle(ChallengeCreateNotification notification, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < notification.Challenge.Spec.HideUntil)
        {
            logger.LogDebug("Skipping challenge created message due to HideUntil property.");
            return;
        }
        var dtoChallenge = ChallengeController.ToChallenge(notification.Challenge);
        if (ctfConfig.Start < DateTime.UtcNow) {
            logger.LogDebug("Sending challenge created message to all websocket clients.");
            await webSocketService.PushEventAll("challenge", dtoChallenge);
        } else {
            logger.LogDebug("Sending challenge created message only to admin websocket clients.");
            var adminIds = dbContext.Players
                .Where(p => p.Roles != null && p.Roles.Contains("admin"))
                .Select(p => p.Id)
                .ToHashSet();
            await webSocketService.PushEvent("challenge", dtoChallenge, adminIds.Contains);
        }
    }

    public async Task Handle(ChallengeUpdateNotification notification, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < notification.Challenge.Spec.HideUntil)
        {
            logger.LogDebug("Skipping challenge update message due to HideUntil property.");
            return;
        }
        var dtoChallenge = ChallengeController.ToChallenge(notification.Challenge);
        if (ctfConfig.Start < DateTime.UtcNow) {
            logger.LogDebug("Sending challenge updated message to all websocket clients.");
            await webSocketService.PushEventAll("challenge", dtoChallenge);
        } else {
            logger.LogDebug("Sending challenge updated message only to admin websocket clients.");
            var adminIds = dbContext.Players
                .Where(p => p.Roles != null && p.Roles.Contains("admin"))
                .Select(p => p.Id)
                .ToHashSet();
            await webSocketService.PushEvent("challenge", dtoChallenge, adminIds.Contains);
        }
    }
}
