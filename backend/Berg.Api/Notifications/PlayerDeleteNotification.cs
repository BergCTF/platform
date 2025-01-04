using MediatR;

namespace Berg.Api.Notifications;

public class PlayerDeleteNotification : INotification
{
    public required Guid PlayerId { get; set; }
}
