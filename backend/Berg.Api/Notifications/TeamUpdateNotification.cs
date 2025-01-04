using Berg.Api.Models.V2;
using MediatR;

namespace Berg.Api.Notifications;

public class TeamUpdateNotification : INotification
{
    public required Team Team { get; set; }
}
