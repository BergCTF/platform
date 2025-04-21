using Berg.Api.Models;
using MediatR;

namespace Berg.Api.Notifications;

public class TeamUpdateNotification : INotification
{
    public required Team Team { get; set; }
}
