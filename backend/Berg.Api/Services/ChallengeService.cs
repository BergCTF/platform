using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Berg.Api.Configuration;
using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
using Berg.Api.CustomResources.Cilium;
using Berg.Api.CustomResources.GatewayApi;
using Berg.Api.Models;
using Berg.Api.Notifications;
using k8s;
using k8s.Autorest;
using k8s.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Berg.Api.Services;

public interface IChallengeService
{
    Task<IEnumerable<V1Challenge>> GetChallenges(CancellationToken cancellationToken);
    Task<V1Challenge?> GetChallenge(string challengeName, CancellationToken cancellationToken);
    Task CheckNewlyUnhiddenChallenges(TimeSpan window, CancellationToken cancellationToken);
}

public class ChallengeService(
    ILogger<ChallengeService> logger,
    IDynamicFlagExecutableService dynamicFlagExecutableService,
    BergMetrics metrics,
    Db.BergDbContext dbContext,
    Kubernetes kubernetes,
    KubernetesClientConfiguration kubernetesConfig,
    InfraConfig infraConfig,
    IMediator mediator) :
    IChallengeService
{
    public const string ManagedByLabel = "app.kubernetes.io/managed-by";
    public const string ComponentLabel = "app.kubernetes.io/component";
    public const string PlayerIdLabel = "berg.norelect.ch/player-id";
    public const string InstanceIdLabel = "berg.norelect.ch/instance-id";
    public const string ChallengeLabel = "berg.norelect.ch/challenge";
    public const string ContainerLabel = "berg.norelect.ch/container";
    public const string HostnameLabel = "berg.norelect.ch/hostname";

    public static readonly ImmutableDictionary<string, string> ChallengeNamespaceLabelSelector = new Dictionary<string, string>
    {
        { ManagedByLabel, "berg" },
        { ComponentLabel, "challenge" },
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, string> ChallengePodLabelSelector = new Dictionary<string, string>
    {
        { ManagedByLabel, "berg" },
        { ComponentLabel, "challenge-pod" },
    }.ToImmutableDictionary();

    private readonly GenericClient _challengeClient = CustomResource.CreateGenericClient<V1Challenge>(kubernetes, false);
    private readonly GenericClient _httpRouteClient = CustomResource.CreateGenericClient<V1HTTPRoute>(kubernetes, false);
    private readonly GenericClient _tlsRouteClient = CustomResource.CreateGenericClient<V1TLSRoute>(kubernetes, false);
    private readonly GenericClient _ciliumNetworkPolicyClient = CustomResource.CreateGenericClient<V2CiliumNetworkPolicy>(kubernetes, false);

    public async Task<IEnumerable<V1Challenge>> GetChallenges(CancellationToken cancellationToken)
    {
        return (await _challengeClient
            .ListNamespacedAsync<CustomResourceList<V1Challenge>>(kubernetesConfig.Namespace, cancel: cancellationToken)).Items;
    }

    public async Task<V1Challenge?> GetChallenge(string challengeName, CancellationToken cancellationToken)
    {
        try
        {
            return await _challengeClient.ReadNamespacedAsync<V1Challenge>(kubernetesConfig.Namespace, challengeName, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task CheckNewlyUnhiddenChallenges(TimeSpan window, CancellationToken cancellationToken)
    {
        using var activity = Constants.BergActivitySource.StartActivity();

        var challengeList = (await _challengeClient
            .ListNamespacedAsync<CustomResourceList<V1Challenge>>(kubernetesConfig.Namespace, cancel: cancellationToken)).Items;

        var now = DateTimeOffset.UtcNow;
        var pastWindow = now.Subtract(window);
        foreach (var unhiddenChallenge in challengeList
            .Where(c => c.Spec.HideUntil != null && c.Spec.HideUntil.Value <= now && pastWindow <= c.Spec.HideUntil.Value))
        {
            await mediator.Publish(new ChallengeUnhideNotification
            {
                Challenge = unhiddenChallenge,
            }, cancellationToken);
        }
    }

    public static string ToLabelSelector(IDictionary<string, string> labelSelector)
    {
        var sb = new StringBuilder();
        var pairs = labelSelector.ToArray();
        for (var i = 0; i < pairs.Length; i++)
        {
            if (i != 0)
                sb.Append(',');
            var pair = pairs[i];
            sb.Append(pair.Key);
            sb.Append('=');
            sb.Append(pair.Value);
        }
        return sb.ToString();
    }
}
