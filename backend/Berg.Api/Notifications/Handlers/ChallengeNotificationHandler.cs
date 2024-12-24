using Berg.Api.CustomResources.Berg;
using Berg.Api.Db;
using k8s.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Berg.Api.Notifications.Handlers;

public class ChallengeNotificationHandler(BergDbContext bergDbContext) :
    INotificationHandler<ChallengeCreateNotification>,
    INotificationHandler<ChallengeUpdateNotification>
{
    public async Task Handle(ChallengeUpdateNotification notification, CancellationToken cancellationToken)
    {
        await HandleChallengeChange(notification.Challenge, cancellationToken);
    }

    public async Task Handle(ChallengeCreateNotification notification, CancellationToken cancellationToken)
    {
        await HandleChallengeChange(notification.Challenge, cancellationToken);
    }

    public async Task HandleChallengeChange(V1Challenge challenge, CancellationToken cancellationToken)
    {
        using var activity = Constants.BergActivitySource.StartActivity();

        var challengeName = challenge.Name();
        var chall = bergDbContext.Challenges.SingleOrDefaultAsync(c => c.Name == challengeName, cancellationToken);
        if(chall == null) {
            bergDbContext.Challenges.Add(new Db.Challenge { Name = challengeName });
            await bergDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
