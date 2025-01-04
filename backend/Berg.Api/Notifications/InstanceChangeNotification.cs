using Berg.Api.Models.V2;
using MediatR;

namespace Berg.Api.Notifications;

public record InstanceChangeNotification : INotification
{
    public Guid PlayerId { get; set; }
    public required Instance Instance { get; set; }
}
