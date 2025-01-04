using Berg.Api.Configuration;
using Berg.Api.Controllers.V2;
using Berg.Api.CustomResources.Berg;
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
    INotificationHandler<PlayerDeleteNotification>,
    INotificationHandler<TeamCreateNotification>,
    INotificationHandler<TeamUpdateNotification>,
    INotificationHandler<TeamDeleteNotification>,
    INotificationHandler<PageCreateNotification>,
    INotificationHandler<PageUpdateNotification>,
    INotificationHandler<ChallengeCreateNotification>,
    INotificationHandler<ChallengeUnhideNotification>,
    INotificationHandler<ChallengeUpdateNotification>,
    INotificationHandler<InstanceChangeNotification>
{
    public async Task Handle(SolveNotification solve, CancellationToken cancellationToken)
    {
        var dtoSolve = new Models.V2.Solve
        {
            Id = solve.Id,
            PlayerId = solve.PlayerId,
            ChallengeName = solve.Challenge,
            SolvedAt = solve.SolvedAt
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

    public async Task Handle(PlayerDeleteNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending player delete message to websocket clients.");
        await webSocketService.PushEventAll("player-delete", notification.PlayerId);
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

    public async Task Handle(TeamDeleteNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending team delete message to websocket clients.");
        await webSocketService.PushEventAll("team-delete", notification.TeamId);
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
        await HandleChallengeChange(notification.Challenge, cancellationToken);
    }

    public async Task Handle(ChallengeUnhideNotification notification, CancellationToken cancellationToken)
    {
        await HandleChallengeChange(notification.Challenge, cancellationToken);
    }

    public async Task Handle(ChallengeUpdateNotification notification, CancellationToken cancellationToken)
    {
        await HandleChallengeChange(notification.Challenge, cancellationToken);
    }

    private async Task HandleChallengeChange(V1Challenge challenge, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < challenge.Spec.HideUntil)
        {
            logger.LogDebug("Skipping challenge message due to HideUntil property.");
            return;
        }
        var dtoChallenge = ChallengeController.ToChallenge(challenge);
        if (ctfConfig.Start < DateTime.UtcNow) {
            logger.LogDebug("Sending challenge message to all websocket clients.");
            await webSocketService.PushEventAll("challenge", dtoChallenge);
        } else {
            logger.LogDebug("Sending challenge message only to admin websocket clients.");
            var adminIds = dbContext.Players
                .Where(p => p.Roles != null && p.Roles.Contains(Constants.Roles.Admin))
                .Select(p => p.Id)
                .ToHashSet();
            await webSocketService.PushEvent("challenge", dtoChallenge, adminIds.Contains);
        }
    }

    public async Task Handle(InstanceChangeNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending instance change message.");
        var playerIdsToNotify = dbContext.Players
            .Where(p => p.Roles != null && p.Roles.Contains(Constants.Roles.Admin))
            .Select(p => p.Id)
            .ToHashSet();
        playerIdsToNotify.Add(notification.PlayerId);
        await webSocketService.PushEvent("instance", notification.Instance, playerIdsToNotify.Contains);
    }
}
