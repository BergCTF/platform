using Berg.Api.Db;
using MediatR;

namespace Berg.Api.Notifications;

public class PlayerUpdateNotification : INotification
{
    public required Player DbPlayer { get; set; }
}
