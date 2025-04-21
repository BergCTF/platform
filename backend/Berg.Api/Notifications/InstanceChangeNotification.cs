using Berg.Api.Models;
using MediatR;

namespace Berg.Api.Notifications;

public record InstanceChangeNotification : INotification
{
    public required Instance Instance { get; set; }
}
