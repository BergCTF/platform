using Berg.Api.CustomResources.Berg;
using MediatR;

namespace Berg.Api.Notifications;

public class PageUpdateNotification : INotification
{
    public required V1Page Page { get; set; }
}
