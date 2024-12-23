using Berg.Api.CustomResources.Berg;
using MediatR;

namespace Berg.Api.Notifications;

public class ChallengeCreateNotification : INotification
{
    public required V1Challenge Challenge { get; set; }
}
