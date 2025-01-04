using MediatR;

namespace Berg.Api.Notifications;

public class TeamDeleteNotification : INotification
{
    public required Guid TeamId { get; set; }
}
