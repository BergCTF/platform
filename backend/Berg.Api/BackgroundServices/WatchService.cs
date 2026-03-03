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
            WithRetry(() => WatchInstanceNamespaces(cancellationToken), cancellationToken),
            WithRetry(() => WatchInstancePods(cancellationToken), cancellationToken),
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

    private async Task WatchInstanceNamespaces(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchInstanceNamespaces started");
        using var scope = serviceScopeFactory.CreateScope();
        var challengeService = scope.ServiceProvider.GetRequiredService<IChallengeService>();
        await foreach (var (type, item) in  kubernetes.CoreV1.WatchListNamespaceAsync(labelSelector: ChallengeService.ToLabelSelector(ChallengeService.ChallengeNamespaceLabelSelector), cancellationToken: cancellationToken))
        {
            logger.LogDebug("{} was {}", item.Name(), type);
            var playerId = Guid.Parse(item.GetLabel(ChallengeService.PlayerIdLabel));
            var instance = await challengeService.GetChallengeInstance(playerId, cancellationToken);
            await mediator.Publish(new InstanceChangeNotification
            {
                Instance = instance,
            }, cancellationToken);
        }
        logger.LogInformation("WatchInstanceNamespaces stopped");
    }

    private async Task WatchInstancePods(CancellationToken cancellationToken)
    {
        logger.LogInformation("WatchInstances started");
        using var scope = serviceScopeFactory.CreateScope();
        var challengeService = scope.ServiceProvider.GetRequiredService<IChallengeService>();
        var labelSelector = ChallengeService.ChallengePodLabelSelector;
        await foreach (var (type, item) in kubernetes.CoreV1.WatchListPodForAllNamespacesAsync(labelSelector: ChallengeService.ToLabelSelector(labelSelector), cancellationToken: cancellationToken))
        {
            if (type == WatchEventType.Added)
                continue;
            logger.LogDebug("{} was {}", item.Name(), type);
            var playerId = Guid.Parse(item.GetLabel(ChallengeService.PlayerIdLabel));
            var instance = await challengeService.GetChallengeInstance(playerId, cancellationToken);
            await mediator.Publish(new InstanceChangeNotification
            {
                Instance = instance,
            }, cancellationToken);
        }
        logger.LogInformation("WatchInstances stopped");
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