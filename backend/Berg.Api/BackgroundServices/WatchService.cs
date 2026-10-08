using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
using Berg.Api.Models;
using Berg.Api.Notifications;
using Berg.Api.Services;
using k8s;
using k8s.Models;
using MediatR;

namespace Berg.Api.BackgroundServices;

public class WatchService(
    ILogger<RefreshService> logger,
    IServiceScopeFactory serviceScopeFactory,
    Kubernetes kubernetes,
    KubernetesClientConfiguration kubernetesConfig,
    IMediator mediator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchService started");

        await Task.WhenAll([
            WithRetry(() => WatchConfig(cancellationToken), cancellationToken),
            WithRetry(() => WatchChallenges(cancellationToken), cancellationToken),
            WithRetry(() => WatchPages(cancellationToken), cancellationToken),
            WithRetry(() => WatchInstances(cancellationToken), cancellationToken),
        ]);

        logger.LogInformation("WatchService stopped");
    }

    private async Task WatchConfig(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchConfig started");
        await foreach (var (type, item) in kubernetes.CoreV1.WatchListNamespacedConfigMapAsync(kubernetesConfig.Namespace, cancellationToken: cancellationToken))
        {
            logger.LogDebug("ConfigMap {} was {}", item.Name(), type);
        }
        logger.LogInformation("WatchConfig stopped");
    }

    private async Task WatchChallenges(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchChallenges started");
        var challenge = new V1Challenge();
        using var challengeListResponse = kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<CustomResourceList<V1Challenge>>(challenge.Group, challenge.Version, kubernetesConfig.Namespace, challenge.Plural, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in challengeListResponse.WatchAsync<V1Challenge, CustomResourceList<V1Challenge>>(cancellationToken: cancellationToken))
        {
            logger.LogDebug("Challenge {} was {}", item.Name(), type);
            if (type == WatchEventType.Added)
            {
                await mediator.Publish(new ChallengeCreateNotification
                {
                    Challenge = item
                }, cancellationToken);
            }
            else if (type == WatchEventType.Modified)
            {
                await mediator.Publish(new ChallengeUpdateNotification
                {
                    Challenge = item
                }, cancellationToken);
            }
        }
        logger.LogInformation("WatchChallenges stopped");
    }

    private async Task WatchPages(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchPages started");
        var page = new V1Page();
        using var pageListResponse = kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<CustomResourceList<V1Page>>(page.Group, page.Version, kubernetesConfig.Namespace, page.Plural, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in pageListResponse.WatchAsync<V1Page, CustomResourceList<V1Page>>(cancellationToken: cancellationToken))
        {
            logger.LogDebug("Page {} was {}", item.Name(), type);
            if (type == WatchEventType.Added)
            {
                await mediator.Publish(new PageCreateNotification
                {
                    Page = item
                }, cancellationToken);
            }
            else if (type == WatchEventType.Modified)
            {
                await mediator.Publish(new PageUpdateNotification
                {
                    Page = item
                }, cancellationToken);
            }
        }
        logger.LogInformation("WatchPages stopped");
    }

    private async Task WatchInstances(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchInstances started");
        using var scope = serviceScopeFactory.CreateScope();
        var challengeService = scope.ServiceProvider.GetRequiredService<IChallengeService>();

        var challengeInstance = new V1ChallengeInstance();
        using var challengeInstanceListResponse = kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<CustomResourceList<V1ChallengeInstance>>(challengeInstance.Group, challengeInstance.Version, kubernetesConfig.Namespace, challengeInstance.Plural, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in challengeInstanceListResponse.WatchAsync<V1ChallengeInstance, CustomResourceList<V1ChallengeInstance>>(cancellationToken: cancellationToken))
        {

            logger.LogDebug("ChallengeInstance {} was {}", item.Name(), type);
            if (type == WatchEventType.Added)
            {
                await mediator.Publish(new InstanceChangeNotification
                {
                    Player = item.Spec.OwnerId,
                    Instance = Instance.FromCR(item)
                }, cancellationToken);
            }
            else if (type == WatchEventType.Modified)
            {
                await mediator.Publish(new InstanceChangeNotification
                {
                    Player = item.Spec.OwnerId,
                    Instance = Instance.FromCR(item)
                }, cancellationToken);
            }
            else if (type == WatchEventType.Deleted)
            {
                // Push a terminated instance (PlayerId set) instead of null, so that the
                // frontend can reset the affected player's instance state to "not running"
                // and other (admin) clients can safely ignore the event.
                await mediator.Publish(new InstanceChangeNotification
                {
                    Player = item.Spec.OwnerId,
                    Instance = new Instance
                    {
                        Id = item.Status?.InstanceId,
                        PlayerId = item.Spec.OwnerId,
                        ChallengeName = item.Spec.ChallengeRef.Name,
                        InstanceState = InstanceState.None
                    }
                }, cancellationToken);
            }

        }
        logger.LogInformation("WatchInstanceNamespaces stopped");
    }

    private async Task WithRetry(Func<Task> action, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await action();
            }
            catch (TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug("WithRetry did not retry because the task was cancelled");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WithRetry swallowed an exception");
            }
        }
    }
}
