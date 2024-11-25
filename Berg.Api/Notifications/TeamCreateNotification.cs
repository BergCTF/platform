using Berg.Api.Models.V2;
using MediatR;

namespace Berg.Api.Notifications;

public class TeamCreateNotification : INotification
{
    public required Team ModelTeam { get; set; }
}
