using Berg.Api.Db;
using Berg.Api.Notifications;
using Berg.Api.Services;
using MediatR;

namespace Berg.Api.Handlers;

public class RefreshScoresSolveNotificationHandler(BergDbContext dbContext,
    ScoringService scoringService) : INotificationHandler<SolveNotification>
{
    public Task Handle(SolveNotification solve, CancellationToken cancellationToken)
    {
        scoringService.RefreshScores(dbContext);
        return Task.CompletedTask;
    }
}
