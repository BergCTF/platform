using Berg.Api.CustomResources.Berg;
using Berg.Api.Db;
using k8s.Models;
using MediatR;

namespace Berg.Api.Notifications.Handlers;

public class ChallengeNotificationHandler(BergDbContext dbContext) :
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

    public Task HandleChallengeChange(V1Challenge challenge, CancellationToken cancellationToken)
    {
        using var activity = Constants.BergActivitySource.StartActivity();

        var challengeName = challenge.Name();
        var chall = dbContext.Challenges.SingleOrDefault(c => c.Name == challengeName);
        if(chall == null) {
            dbContext.Challenges.Add(new Challenge { Name = challengeName });
            dbContext.SaveChanges();
        }
        return Task.CompletedTask;
    }
}
