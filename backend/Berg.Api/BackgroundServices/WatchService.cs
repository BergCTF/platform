using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
using Berg.Api.Notifications;
using k8s;
using k8s.Models;
using MediatR;

namespace Berg.Api.BackgroundServices;

public class WatchService(
    ILogger<RefreshService> logger,
    Kubernetes kubernetes,
    IMediator mediator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchService started");

        await Task.WhenAll([
            WatchConfig(cancellationToken),
            WatchChallenges(cancellationToken),
            WatchPages(cancellationToken),
        ]);

        logger.LogInformation("WatchService stopped");
    }

    private async Task WatchConfig(CancellationToken cancellationToken) {
        var bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        using var configMapListResponse = kubernetes.CoreV1.ListNamespacedConfigMapWithHttpMessagesAsync(bergNamespace, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in configMapListResponse.WatchAsync<V1ConfigMap, V1ConfigMapList>(cancellationToken: cancellationToken))
        {
            logger.LogInformation("ConfigMap {} was {}", item.Name(), type);
        }
        logger.LogInformation("WatchConfig stopped");
    }

    private async Task WatchChallenges(CancellationToken cancellationToken) {
        var bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        var challenge = new V1Challenge();
        using var challengeListResponse = kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<CustomResourceList<V1Challenge>>(challenge.Group, challenge.Version, bergNamespace, challenge.Plural, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in challengeListResponse.WatchAsync<V1Challenge, CustomResourceList<V1Challenge>>(cancellationToken: cancellationToken))
        {
            logger.LogInformation("Challenge {} was {}", item.Name(), type);
            if (type == WatchEventType.Added) {
                await mediator.Publish(new ChallengeCreateNotification
                {
                    Challenge = item
                }, cancellationToken);
            } else if (type == WatchEventType.Modified) {
                await mediator.Publish(new ChallengeUpdateNotification
                {
                    Challenge = item
                }, cancellationToken);
            }
        }
        logger.LogInformation("WatchChallenges stopped");
    }

    private async Task WatchPages(CancellationToken cancellationToken) {
        var bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        var page = new V1Page();
        using var pageListResponse = kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<CustomResourceList<V1Page>>(page.Group, page.Version, bergNamespace, page.Plural, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in pageListResponse.WatchAsync<V1Page, CustomResourceList<V1Page>>(cancellationToken: cancellationToken))
        {
            logger.LogInformation("Page {} was {}", item.Name(), type);
            if (type == WatchEventType.Added) {
                await mediator.Publish(new PageCreateNotification
                {
                    Page = item
                }, cancellationToken);
            } else if (type == WatchEventType.Modified) {
                await mediator.Publish(new PageUpdateNotification
                {
                    Page = item
                }, cancellationToken);
            }
        }
        logger.LogInformation("WatchPages stopped");
    }
}