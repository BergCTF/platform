using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
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
    IMediator mediator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchService started");

        await Task.WhenAll([
            WithRetry(() => WatchConfig(cancellationToken), cancellationToken),
            WithRetry(() => WatchChallenges(cancellationToken), cancellationToken),
            WithRetry(() => WatchPages(cancellationToken), cancellationToken),
            WithRetry(() => WatchInstanceNamespaces(cancellationToken), cancellationToken),
            WithRetry(() => WatchInstancePods(cancellationToken), cancellationToken),
        ]);

        logger.LogInformation("WatchService stopped");
    }

    private async Task WatchConfig(CancellationToken cancellationToken) {
        logger.LogInformation("WatchConfig started");
        var bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        using var configMapListResponse = kubernetes.CoreV1.ListNamespacedConfigMapWithHttpMessagesAsync(bergNamespace, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in configMapListResponse.WatchAsync<V1ConfigMap, V1ConfigMapList>(cancellationToken: cancellationToken))
        {
            logger.LogDebug("ConfigMap {} was {}", item.Name(), type);
        }
        logger.LogInformation("WatchConfig stopped");
    }

    private async Task WatchChallenges(CancellationToken cancellationToken) {
        logger.LogInformation("WatchChallenges started");
        var bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        var challenge = new V1Challenge();
        using var challengeListResponse = kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<CustomResourceList<V1Challenge>>(challenge.Group, challenge.Version, bergNamespace, challenge.Plural, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in challengeListResponse.WatchAsync<V1Challenge, CustomResourceList<V1Challenge>>(cancellationToken: cancellationToken))
        {
            logger.LogDebug("Challenge {} was {}", item.Name(), type);
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
        logger.LogInformation("WatchPages started");
        var bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
        var page = new V1Page();
        using var pageListResponse = kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<CustomResourceList<V1Page>>(page.Group, page.Version, bergNamespace, page.Plural, watch: true, cancellationToken: cancellationToken);
        await foreach (var (type, item) in pageListResponse.WatchAsync<V1Page, CustomResourceList<V1Page>>(cancellationToken: cancellationToken))
        {
            logger.LogDebug("Page {} was {}", item.Name(), type);
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

    private async Task WatchInstanceNamespaces(CancellationToken cancellationToken) {
        logger.LogInformation("WatchInstanceNamespaces started");
        using var scope = serviceScopeFactory.CreateScope();
        var challengeService = scope.ServiceProvider.GetRequiredService<IChallengeService>();
        using var nsListResponse = kubernetes.CoreV1.ListNamespaceWithHttpMessagesAsync(watch: true, labelSelector: ChallengeService.ToLabelSelector(ChallengeService.ChallengeNamespaceLabelSelector), cancellationToken: cancellationToken);
        await foreach (var (type, item) in nsListResponse.WatchAsync<V1Namespace, V1NamespaceList>(cancellationToken: cancellationToken))
        {
            logger.LogDebug("{} was {}", item.Name(), type);
            var playerId = Guid.Parse(item.GetLabel(ChallengeService.PlayerIdLabel));
            var instance = await challengeService.GetChallengeInstance(playerId, cancellationToken);
            await mediator.Publish(new InstanceChangeNotification
            {
                PlayerId = playerId,
                Instance = instance,
            }, cancellationToken);
        }
        logger.LogInformation("WatchInstanceNamespaces stopped");
    }

    private async Task WatchInstancePods(CancellationToken cancellationToken) {
        logger.LogInformation("WatchInstances started");
        using var scope = serviceScopeFactory.CreateScope();
        var challengeService = scope.ServiceProvider.GetRequiredService<IChallengeService>();
        var labelSelector = ChallengeService.ChallengePodLabelSelector;
        using var podListResponse = kubernetes.CoreV1.ListPodForAllNamespacesWithHttpMessagesAsync(watch: true, labelSelector: ChallengeService.ToLabelSelector(labelSelector), cancellationToken: cancellationToken);
        await foreach (var (type, item) in podListResponse.WatchAsync<V1Pod, V1PodList>(cancellationToken: cancellationToken))
        {
            if (type == WatchEventType.Added)
                continue;
            logger.LogDebug("{} was {}", item.Name(), type);
            var playerId = Guid.Parse(item.GetLabel(ChallengeService.PlayerIdLabel));
            var instance = await challengeService.GetChallengeInstance(playerId, cancellationToken);
            await mediator.Publish(new InstanceChangeNotification
            {
                PlayerId = playerId,
                Instance = instance,
            }, cancellationToken);
        }
        logger.LogInformation("WatchInstances stopped");
    }

    private async Task WithRetry(Func<Task> action, CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await action();
            }
            catch (TaskCanceledException) {
                if (cancellationToken.IsCancellationRequested) {
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