using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Notifications;
using Berg.Api.Services;
using MediatR;

namespace Berg.Api.Handlers;

public class WebSocketSolveNotificationHandler(WebSocketService webSocketService,
    CtfConfig ctfConfig,
    BergDbContext dbContext,
    ILogger<WebSocketSolveNotificationHandler> logger) : INotificationHandler<SolveNotification>
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
            logger.LogDebug("Only messaging specific teams due to freeze.");
            var teamPlayerIds = dbContext.Players.Where(p => p.TeamId == solve.TeamId).Select(p => p.Id).ToHashSet();
            await webSocketService.PushEvent("solve", dtoSolve, teamPlayerIds.Contains);
        }
        else
        {
            logger.LogDebug("Only messaging specific players due to freeze.");
            await webSocketService.PushEvent("solve", dtoSolve, p => solve.PlayerId == p);
        }
    }
}
