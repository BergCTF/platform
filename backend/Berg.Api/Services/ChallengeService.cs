using System.Collections.Immutable;
using System.Text;
using Berg.Api.Configuration;
using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
using Berg.Api.CustomResources.Cilium;
using Berg.Api.CustomResources.GatewayApi;
using Berg.Api.Models.V2;
using Berg.Api.Notifications;
using k8s;
using k8s.Autorest;
using k8s.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Berg.Api.Services;

public interface IChallengeService
{
    IEnumerable<V1Challenge> GetChallenges();
    V1Challenge? GetChallengeConfig(string challengeName);
    Task CheckChallengeInstanceTimeout(CancellationToken cancel);
    Task CheckNewlyUnhiddenChallenges(TimeSpan window, CancellationToken cancel);
    Task<Instance> GetChallengeInstance(Guid playerId, CancellationToken cancel);
    Task<List<Instance>> GetChallengeInstances(CancellationToken cancel);
    Task<Instance> StartChallengeInstance(Guid playerId, V1Challenge challenge, CancellationToken cancel);
    Task<Instance> StopChallengeInstance(Guid playerId, CancellationToken cancel);
}

public class ChallengeService(
    ILogger<ChallengeService> logger,
    Kubernetes kubernetes,
    CtfConfig ctfConfig,
    InfraConfig infraConfig,
    IMediator mediator) :
    IChallengeService
{
    public const string ManagedByLabel      = "app.kubernetes.io/managed-by";
    public const string ComponentLabel      = "app.kubernetes.io/component";
    public const string PlayerIdLabel       = "berg.norelect.ch/player-id";
    public const string ChallengeLabel      = "berg.norelect.ch/challenge";
    public const string ContainerLabel      = "berg.norelect.ch/container";
    public const string HostnameLabel       = "berg.norelect.ch/hostname";

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
    private readonly GenericClient _tlsRouteClient = CustomResource.CreateGenericClient<V1Alpha2TLSRoute>(kubernetes, false);
    private readonly GenericClient _ciliumNetworkPolicyClient = CustomResource.CreateGenericClient<V2CiliumNetworkPolicy>(kubernetes, false);
    private readonly string _bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    private Dictionary<string, V1Challenge> challengeCache = [];

    public IEnumerable<V1Challenge> GetChallenges()
    {
        var now = DateTime.UtcNow;
        return challengeCache.Values
            .Where(c => c.Spec.HideUntil == null || c.Spec.HideUntil <= now)
            .ToList();
    }

    public V1Challenge? GetChallengeConfig(string challengeName)
    {
        var now = DateTime.UtcNow;
        return challengeCache.Values
            .FirstOrDefault(c => c.Name() == challengeName);
    }

    public async Task CheckNewlyUnhiddenChallenges(TimeSpan window, CancellationToken cancel)
    {
        using var activity = Constants.BergActivitySource.StartActivity();

        var challengeList = await _challengeClient
            .ListNamespacedAsync<CustomResourceList<V1Challenge>>(_bergNamespace, cancel);

        challengeCache = challengeList.Items
            .ToDictionary(c => c.Name(), c => c);

        var now = DateTime.UtcNow;
        var pastWindow = now.Subtract(window);
        foreach (var unhiddenChallenge in challengeCache.Values
            .Where(c => c.Spec.HideUntil != null && c.Spec.HideUntil.Value <= now && pastWindow <= c.Spec.HideUntil.Value))
        {
            await mediator.Publish(new ChallengeUnhideNotification
            {
                Challenge = unhiddenChallenge,
            }, cancel);
        }
    }
    public async Task CheckChallengeInstanceTimeout(CancellationToken cancel)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(ChallengeNamespaceLabelSelector),
            cancellationToken: cancel);

        var maxAge = DateTime.UtcNow.Subtract(infraConfig.ChallengeInstanceTimeout);
        foreach (var ns in nsList.Items.Where(n => n.Metadata.CreationTimestamp < maxAge))
        {
            logger.LogInformation("Removing {} because it reached the instance timeout", ns.Name());
            await kubernetes.DeleteNamespaceAsync(ns.Name(), cancellationToken: cancel);
        }
    }
    public async Task<List<Instance>> GetChallengeInstances(CancellationToken cancel)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(ChallengeNamespaceLabelSelector),
            cancellationToken: cancel);

        var playersWithActiveInstances = nsList.Items.Select(ns => Guid.Parse(ns.GetLabel(PlayerIdLabel)));
        var instances = new List<Instance>();
        foreach(var playerId in playersWithActiveInstances)
        {
            instances.Add(await GetChallengeInstance(playerId, cancel));
        }
        return instances;
    }

    public async Task<Instance> GetChallengeInstance(Guid playerId, CancellationToken cancel)
    {
        using var activity = Constants.BergActivitySource.StartActivity();

        var labelSelector = new Dictionary<string, string>(ChallengeNamespaceLabelSelector)
        {
            { PlayerIdLabel, playerId.ToString() },
        };
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);

        var ns = nsList.Items.FirstOrDefault();
        if (ns == null)
            return new Instance { InstanceState = InstanceState.None };

        var challengeName = ns.GetLabel(ChallengeLabel);
        if (ns.Status.Phase == "Terminating")
            return new Instance
            {
                Name = challengeName,
                InstanceState = InstanceState.Terminating
            };

        var challenge = await _challengeClient.ReadNamespacedAsync<V1Challenge>(_bergNamespace, challengeName, cancel);

        var podList = await kubernetes.ListNamespacedPodAsync(ns.Name(), cancellationToken: cancel);
        if (podList.Items.Any(p => p.Status.Phase != "Running") || podList.Items.Count == 0)
           return new Instance
           {
               Name = challengeName,
               InstanceState = InstanceState.Starting
           };

        var serviceList = await kubernetes.ListNamespacedServiceAsync(ns.Name(), cancellationToken: cancel);
        var httpRouteList = await _httpRouteClient
            .ListNamespacedAsync<CustomResourceList<V1HTTPRoute>>(ns.Name(), cancel);
        var tlsRouteList = await _tlsRouteClient
            .ListNamespacedAsync<CustomResourceList<V1Alpha2TLSRoute>>(ns.Name(), cancel);

        var services = new List<Service>();
        foreach (var container in challenge.Spec.Containers ?? [])
        {
            foreach (var port in container.Ports ?? [])
            {
                if (port.Type == V1ChallengePortType.InternalPort)
                    continue;
                var service = new Service
                {
                    Name = port.Name,
                    Hostname = infraConfig.ChallengeDomain,
                    AppProtocol = port.AppProtocol,
                    Protocol = port.Protocol,
                    Tls = true,
                };

                if (port.Type == V1ChallengePortType.PublicPort)
                {
                    var serviceName = $"{container.Hostname}-node-port";
                    var infraService = serviceList.Items.FirstOrDefault(s => s.Name() == serviceName);
                    var infraPort = infraService?.Spec.Ports.FirstOrDefault(p => p.Port == port.Port);
                    service.Port = infraPort?.NodePort ?? 0;
                    service.Tls = false;
                }
                else if (port.Type == V1ChallengePortType.PublicHttpRoute)
                {
                    var ingress = httpRouteList.Items
                        .FirstOrDefault(i => i.Name() == $"{container.Hostname}-{port.Port}");
                    service.Hostname = (ingress?.GetLabel(HostnameLabel) ?? "<loading>") + "." + infraConfig.ChallengeDomain;
                    service.Port = infraConfig.ChallengeHttpPort;
                }
                else if (port.Type == V1ChallengePortType.PublicTlsRoute)
                {
                    var ingress = tlsRouteList.Items
                        .FirstOrDefault(i => i.Name() == $"{container.Hostname}-{port.Port}");
                    service.Hostname = (ingress?.GetLabel(HostnameLabel) ?? "<loading>") + "." +infraConfig.ChallengeDomain;
                    service.Port = infraConfig.ChallengeTlsPort;
                }
                services.Add(service);
            }
        }

        return new Instance
        {
            Name = challengeName,
            InstanceState = InstanceState.Running,
            Services = services,
            Timeout = ns.CreationTimestamp()?.Add(infraConfig.ChallengeInstanceTimeout)
        };
    }

    public async Task<Instance> StartChallengeInstance(Guid playerId, V1Challenge challenge, CancellationToken cancel)
    {
        var challengeName = challenge.Metadata.Name;
        var labelSelector = new Dictionary<string, string>(ChallengeNamespaceLabelSelector)
        {
            { PlayerIdLabel, playerId.ToString() }
        };
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);
        if (nsList.Items.Any())
        {
            var instance = await GetChallengeInstance(playerId, cancel);
            logger.LogWarning("Player {PlayerId} tried to start challenge {NewChallenge}, but already had an instance of challenge {OldChallenge} running!", playerId, challengeName, instance.Name);
            return instance;
        };

        if ((challenge.Spec.Containers?.Count ?? 0) == 0)
            throw new ArgumentException("Challenge can't be instantiated");

        var ns = await kubernetes.CreateNamespaceAsync(new V1Namespace
        {
            Metadata = new V1ObjectMeta
            {
                Name = $"challenge-{playerId}",
                Labels = new Dictionary<string, string>(ChallengeNamespaceLabelSelector)
                {
                    { ChallengeLabel, challengeName },
                    { PlayerIdLabel, playerId.ToString() },
                }
            }
        }, cancellationToken: cancel);

        try
        {
            var imagePullSecret =
                await kubernetes.ReadNamespacedSecretAsync(infraConfig.PullSecretName, _bergNamespace, cancellationToken: cancel);
            await kubernetes.CreateNamespacedSecretAsync(new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Name = infraConfig.PullSecretName,
                },
                Type = "kubernetes.io/dockerconfigjson",
                Data = imagePullSecret.Data
            }, ns.Name(), cancellationToken: cancel);
        }
        catch (HttpOperationException ex)
        {
            logger.LogWarning("Image pull secret '{}' not found in namespace '{}'", infraConfig.PullSecretName, _bergNamespace);
            logger.LogWarning("Detailed exception for pull secret copy operations: {}", ex);
        }

        var networkPolicy = new V2CiliumNetworkPolicy()
        {
            Metadata = new V1ObjectMeta
            {
                Name = "challenge-network-policy",
            },
            Spec = new V2CiliumNetworkPolicySpec
            {
                EndpointSelector = new V1LabelSelector(),
                Egress =
                [
                    new()
                    {
                        ToEndpoints =
                        [
                            new()
                            {
                                MatchLabels = new Dictionary<string, string>
                                {
                                    { "k8s:io.kubernetes.pod.namespace", "kube-system" },
                                    { "k8s:k8s-app", "kube-dns" },
                                }
                            }
                        ],
                        ToPorts =
                        [
                              new()
                              {
                                  Ports =
                                  [
                                      new()
                                      {
                                          Port = "53"
                                      }
                                  ],
                                  Rules = challenge.Spec.AllowOutboundTraffic ? null : new V2CiliumL7Rule
                                  {
                                      Dns =
                                      [
                                          new()
                                          {
                                              // If outbound traffic is forbidden, only accept
                                              // dns requests for internal services
                                              MatchPattern = $"*.{ns.Name()}.svc.cluster.local.",
                                          }
                                      ]
                                  }
                              }
                        ]
                    },
                    new()
                    {
                        ToEndpoints =
                        [
                            // Allow traffic to other pods in the same namespace
                            // by matching all labels
                            new()
                        ]
                    },
                    new()
                    {
                        // Allow outbound traffic to self using the public ip address
                        // as this is required for OIDC to work if an IDP is deployed within
                        // a challenge.
                        ToEntities = [CiliumEntity.Host],
                        ToPorts =
                        [
                            new()
                            {
                                Ports =
                                [
                                    new()
                                    {
                                        Port = infraConfig.ChallengeHttpPort.ToString()
                                    },
                                    new()
                                    {
                                        Port = infraConfig.ChallengeTlsPort.ToString()
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        if (challenge.Spec.AllowOutboundTraffic)
        {
            networkPolicy.Spec.Egress.Add(new V2CiliumEgressRule
            {
                ToEntities =
                [
                    CiliumEntity.World
                ]
            });
        }

        try
        {
            await _ciliumNetworkPolicyClient.CreateNamespacedAsync(networkPolicy, ns.Name(), cancel);
        }
        catch (HttpOperationException ex)
        {
            logger.LogError("Got exception while creating CiliumNetworkPolicy: {}", ex);
            logger.LogError("Response.Content: {}", ex.Response.Content);
            logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(networkPolicy));
        }

        var serviceEndpoints = new Dictionary<string, string>();

        foreach (var container in challenge.Spec.Containers ?? [])
        {
            var allPorts = container.Ports ?? [];
            if (allPorts.Any())
            {
                var service = new V1Service
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = container.Hostname,
                    },
                    Spec = new V1ServiceSpec
                    {
                        Selector = new Dictionary<string, string>
                        {
                            { ContainerLabel, container.Hostname }
                        },
                        Type = "ClusterIP",
                        Ports = allPorts.Select(p => new V1ServicePort
                        {
                            Name = $"port-{p.Port}",
                            AppProtocol = p.AppProtocol,
                            Port = p.Port,
                            TargetPort = p.Port,
                            Protocol = p.Protocol.ToUpperInvariant(),
                        }).ToList(),
                    }
                };
                foreach (var port in allPorts)
                {
                    if (port.Name == null)
                        continue;
                    serviceEndpoints.Add(port.Name, $"{container.Hostname}:{port.Port}");
                }
                try
                {
                    await kubernetes.CreateNamespacedServiceAsync(service, ns.Name(), cancellationToken: cancel);
                }
                catch (HttpOperationException ex)
                {
                    logger.LogError("Got exception while creating Service: {}", ex);
                    logger.LogError("Response.Content: {}", ex.Response.Content);
                    logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(service));
                }
            }

            var publicPorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.PublicPort)
                .ToList() ?? [];
            if (publicPorts.Any())
            {
                var service = new V1Service
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = $"{container.Hostname}-node-port",
                    },
                    Spec = new V1ServiceSpec
                    {
                        Selector = new Dictionary<string, string>
                        {
                            { ContainerLabel, container.Hostname }
                        },
                        Type = "NodePort",
                        Ports = publicPorts.Select(p => new V1ServicePort
                        {
                            Name = $"port-{p.Port}",
                            AppProtocol = p.AppProtocol,
                            Port = p.Port,
                            TargetPort = p.Port,
                            Protocol = p.Protocol.ToUpperInvariant(),
                        }).ToList(),
                    }
                };
                try
                {
                    await kubernetes.CreateNamespacedServiceAsync(service, ns.Name(), cancellationToken: cancel);
                }
                catch (HttpOperationException ex)
                {
                    logger.LogError("Got exception while creating Service: {}", ex);
                    logger.LogError("Response.Content: {}", ex.Response.Content);
                    logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(service));
                }
            }

            var httpRoutePorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.PublicHttpRoute)
                .ToList() ?? [];
            foreach (var httpRoutePort in httpRoutePorts)
            {
                var serviceGuid = Guid.NewGuid();
                var httpRoute = new V1HTTPRoute
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = $"{container.Hostname}-{httpRoutePort.Port}",
                        NamespaceProperty = ns.Name(),
                        Labels = new Dictionary<string, string>()
                        {
                            { ManagedByLabel, "berg" },
                            { ComponentLabel, "http-route" },
                            { HostnameLabel, $"{serviceGuid}" }
                        }
                    },
                    Spec = new V1HTTPRouteSpec
                    {
                        Hostnames =
                        [
                            $"{serviceGuid}.{infraConfig.ChallengeDomain}"
                        ],
                        ParentRefs =
                        [
                            new()
                            {
                                Kind = "Gateway",
                                Name = infraConfig.GatewayName,
                                SectionName = infraConfig.ChallengeHttpListenerName,
                                Namespace = _bergNamespace,
                            }
                        ],
                        Rules =
                        [
                            new ()
                            {
                                BackendRefs =
                                [
                                    new ()
                                    {
                                        Name = container.Hostname,
                                        Port = httpRoutePort.Port,
                                        Namespace = ns.Name()
                                    }
                                ]
                            }
                        ]
                    }
                };
                if (httpRoutePort.Name != null)
                {
                    serviceEndpoints.Remove(httpRoutePort.Name);
                    serviceEndpoints.Add(httpRoutePort.Name,
                        $"{serviceGuid}.{infraConfig.ChallengeDomain}:{infraConfig.ChallengeHttpPort}");
                }
                try
                {
                    await _httpRouteClient.CreateNamespacedAsync(httpRoute, ns.Name(), cancel: cancel);
                }
                catch (HttpOperationException ex)
                {
                    logger.LogError("Got exception while creating HttpRoute: {}", ex);
                    logger.LogError("Response.Content: {}", ex.Response.Content);
                    logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(httpRoute));
                }
            }

            var tlsRoutePorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.PublicTlsRoute)
                .ToList() ?? [];
            foreach (var tlsRoutePort in tlsRoutePorts)
            {
                var serviceGuid = Guid.NewGuid();
                var tlsRoute = new V1Alpha2TLSRoute()
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = $"{container.Hostname}-{tlsRoutePort.Port}",
                        NamespaceProperty = ns.Name(),
                        Labels = new Dictionary<string, string>
                        {
                            { ManagedByLabel, "berg" },
                            { ComponentLabel, "tls-route" },
                            { HostnameLabel, $"{serviceGuid}" }
                        }
                    },
                    Spec = new V1Alpha2TLSRouteSpec
                    {
                        Hostnames =
                        [
                            $"{serviceGuid}.{infraConfig.ChallengeDomain}"
                        ],
                        ParentRefs =
                        [
                            new()
                            {
                                Kind = "Gateway",
                                Name = infraConfig.GatewayName,
                                SectionName = infraConfig.ChallengeTlsListenerName,
                                Namespace = _bergNamespace,
                            }
                        ],
                        Rules =
                        [
                            new ()
                            {
                                BackendRefs =
                                [
                                    new ()
                                    {
                                        Name = container.Hostname,
                                        Port = tlsRoutePort.Port,
                                        Namespace = ns.Name()
                                    }
                                ]
                            }
                        ]
                    }
                };
                if (tlsRoutePort.Name != null)
                {
                    serviceEndpoints.Remove(tlsRoutePort.Name);
                    serviceEndpoints.Add(tlsRoutePort.Name,
                        $"{serviceGuid}.{infraConfig.ChallengeDomain}:{infraConfig.ChallengeTlsPort}");
                }
                try
                {
                    await _tlsRouteClient.CreateNamespacedAsync(tlsRoute, ns.Name(), cancel: cancel);
                }
                catch (HttpOperationException ex)
                {
                    logger.LogError("Got exception while creating TLSRoute: {}", ex);
                    logger.LogError("Response.Content: {}", ex.Response.Content);
                    logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(tlsRoute));
                }
            }
        }

        foreach (var container in challenge.Spec.Containers ?? [])
        {
            var env = (container.Environment ?? [])
                .Select(e => new V1EnvVar(e.Key, e.Value.ToString()))
                .Concat(serviceEndpoints.Select(e => new V1EnvVar($"{e.Key.ToUpperInvariant()}_ENDPOINT", e.Value)))
                .ToList();
            env.Add(new V1EnvVar("CHALLENGE_NAMESPACE", ns.Name()));
            var podSpec = new V1PodSpec
            {
                RestartPolicy = "Always",
                Hostname = container.Hostname,
                EnableServiceLinks = false,
                AutomountServiceAccountToken = false,
                TerminationGracePeriodSeconds = 0,
                ImagePullSecrets = [new(infraConfig.PullSecretName)],
                Containers =
                [
                    new()
                    {
                        SecurityContext = new V1SecurityContext
                        {
                            Privileged = false,
                            // AllowPrivilegeEscalation has to be set to true to enable setuid or setgid
                            // challenges, as they break without this flag. This should not introduce a security
                            // vulnerability to the cluster as written in the docs:
                            // - https://kubernetes.io/docs/tasks/configure-pod-container/security-context/
                            // - https://www.kernel.org/doc/Documentation/prctl/no_new_privs.txt
                            AllowPrivilegeEscalation = true
                        },
                        Name = container.Hostname,
                        Image = container.Image,
                        ImagePullPolicy = "Always",
                        Resources = container.ResourceLimits != null
                            ? new V1ResourceRequirements
                            {
                                Limits = container.ResourceLimits
                                    .ToDictionary(l => l.Key, l => new ResourceQuantity(l.Value)),
                                Requests = new Dictionary<string, ResourceQuantity>()
                                {
                                    { "cpu", new ResourceQuantity("0") },
                                    { "memory", new ResourceQuantity("1Mi") }
                                }
                            }
                            : null,
                        Env = env,
                        Ports = container.Ports?
                            .Select(p => new V1ContainerPort(p.Port, protocol: p.Protocol.ToUpperInvariant()))
                            .ToList()
                    }
                ],
            };
            var labels = new Dictionary<string, string>(ChallengePodLabelSelector)
            {
                { ChallengeLabel, challengeName },
                { PlayerIdLabel, playerId.ToString() },
                { ContainerLabel, container.Hostname }
            };
            var deployment = new V1Deployment
            {
                Metadata = new V1ObjectMeta
                {
                    Name = container.Hostname,
                },
                Spec = new V1DeploymentSpec
                {
                    Replicas = 1,
                    Selector = new V1LabelSelector
                    {
                        MatchLabels = labels
                    },
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = container.Hostname,
                            Labels = labels
                        },
                        Spec = podSpec
                    }
                }
            };
            try
            {
                await kubernetes.CreateNamespacedDeploymentAsync(deployment, ns.Name(), cancellationToken: cancel);
            }
            catch (HttpOperationException ex)
            {
                logger.LogError("Got exception while creating Pod: {}", ex);
                logger.LogError("Response.Content: {}", ex.Response.Content);
                logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(deployment));
            }
        }

        logger.LogInformation("Created instance of challenge: {}", challengeName);
        return new Instance { Name = challengeName, InstanceState = InstanceState.Starting };
    }

    public async Task<Instance> StopChallengeInstance(Guid playerId, CancellationToken cancel)
    {
        var labelSelector = new Dictionary<string, string>(ChallengeNamespaceLabelSelector)
        {
            { PlayerIdLabel, playerId.ToString() },
        };
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);
        var ns = nsList.Items.FirstOrDefault();
        if (ns == null)
            return new Instance { InstanceState = InstanceState.None };

        await kubernetes.DeleteNamespaceAsync(ns.Name(), gracePeriodSeconds: 0, cancellationToken: cancel);
        var challengeName = ns.GetLabel(ChallengeLabel);
        logger.LogInformation("Deleted instance of challenge: {}", challengeName);
        return new Instance { Name = challengeName, InstanceState = InstanceState.Terminating };
    }

    public static string ToLabelSelector(IDictionary<string, string> labelSelector)
    {
        var sb = new StringBuilder();
        var pairs = labelSelector.ToArray();
        for (var i = 0; i < pairs.Length; i++)
        {
            if(i != 0)
                sb.Append(',');
            var pair = pairs[i];
            sb.Append(pair.Key);
            sb.Append('=');
            sb.Append(pair.Value);
        }
        return sb.ToString();
    }
}