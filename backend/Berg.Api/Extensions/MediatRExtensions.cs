using MediatR;

namespace Berg.Api.Extensions;

public static class MediatRExtensions
{
    public static void AddMediatR(this WebApplicationBuilder builder)
    {
        builder.Services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<Program>();
            cfg.NotificationPublisherType = typeof(OpenTelemetryTracingTaskWhenAllPublisher);
            cfg.AddOpenBehavior(typeof(OpenTelemetryTracingBehavior<,>));
        });
    }
}

public class OpenTelemetryTracingTaskWhenAllPublisher(
    ILogger<OpenTelemetryTracingTaskWhenAllPublisher> logger
) : INotificationPublisher
{
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        var notificationName = notification.GetType().Name;
        logger.LogTrace("Sending notification of type: {NotificationType}", notificationName);
        using var activity = Constants.BergActivitySource.StartActivity(notificationName);
        var tasks = handlerExecutors
            .Select(handler => ExecuteHandlerCallback(handler, notification, cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task ExecuteHandlerCallback(NotificationHandlerExecutor handler, INotification notification, CancellationToken cancellationToken)
    {
        var handlerName = handler.HandlerInstance.GetType().Name;
        using var activity = Constants.BergActivitySource.StartActivity(handlerName);
        logger.LogTrace("Calling handler: {HandlerType}", handlerName);
        await handler.HandlerCallback(notification, cancellationToken);
        activity?.Stop();
    }
}

public class OpenTelemetryTracingBehavior<TRequest, TResponse>(
    ILogger<OpenTelemetryTracingBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        using var activity = Constants.BergActivitySource.StartActivity(requestName);
        logger.LogTrace("Starting request of type: {RequestType}", requestName);
        var response = await next();
        activity?.Stop();
        logger.LogTrace("Finished request of type: {RequestType}", requestName);
        return response;
    }
}
