using Berg.Api.Models;
using MediatR;

namespace Berg.Api.Notifications;

public class TeamCreateNotification : INotification
{
    public required Team Team { get; set; }
}
