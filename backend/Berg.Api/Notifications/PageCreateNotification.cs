using Berg.Api.CustomResources.Berg;
using MediatR;

namespace Berg.Api.Notifications;

public class PageCreateNotification : INotification
{
    public required V1Page Page { get; set; }
}
