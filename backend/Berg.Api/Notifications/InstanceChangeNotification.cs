using Berg.Api.Models;
using MediatR;

namespace Berg.Api.Notifications;

public record InstanceChangeNotification : INotification
{
    public Guid Player { get; set; }
    public Instance? Instance { get; set; }
}
