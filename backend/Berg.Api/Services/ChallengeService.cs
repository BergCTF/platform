using System.Collections.Immutable;
using System.Security.Cryptography;
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
    Task<IEnumerable<V1Challenge>> GetChallenges(CancellationToken cancellationToken);
    Task<V1Challenge?> GetChallenge(string challengeName, CancellationToken cancellationToken);
    Task CheckChallengeInstanceTimeout(CancellationToken cancellationToken);
    Task CheckNewlyUnhiddenChallenges(TimeSpan window, CancellationToken cancellationToken);
    Task<Instance> GetChallengeInstance(Guid playerId, CancellationToken cancellationToken);
    Task<List<Instance>> GetChallengeInstances(CancellationToken cancellationToken);
    Task<Instance> StartChallengeInstance(Guid playerId, V1Challenge challenge, CancellationToken cancellationToken);
    Task<Instance> StopChallengeInstance(Guid playerId, CancellationToken cancellationToken);
}

public class ChallengeService(
    ILogger<ChallengeService> logger,
    IDynamicFlagExecutableService dynamicFlagExecutableService,
    Db.BergDbContext dbContext,
    Kubernetes kubernetes,
    InfraConfig infraConfig,
    IMediator mediator) :
    IChallengeService
{
    public const string ManagedByLabel      = "app.kubernetes.io/managed-by";
    public const string ComponentLabel      = "app.kubernetes.io/component";
    public const string PlayerIdLabel       = "berg.norelect.ch/player-id";
    public const string InstanceIdLabel     = "berg.norelect.ch/instance-id";
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

    public async Task<IEnumerable<V1Challenge>> GetChallenges(CancellationToken cancellationToken)
    {
        return (await _challengeClient
            .ListNamespacedAsync<CustomResourceList<V1Challenge>>(_bergNamespace, cancellationToken)).Items;
    }

    public async Task<V1Challenge?> GetChallenge(string challengeName, CancellationToken cancellationToken)
    {
        try
        {
            return await _challengeClient.ReadNamespacedAsync<V1Challenge>(_bergNamespace, challengeName, cancellationToken);
        } catch {
            return null;
        }
    }

    public async Task CheckNewlyUnhiddenChallenges(TimeSpan window, CancellationToken cancellationToken)
    {
        using var activity = Constants.BergActivitySource.StartActivity();

        var challengeList = (await _challengeClient
            .ListNamespacedAsync<CustomResourceList<V1Challenge>>(_bergNamespace, cancellationToken)).Items;

        var now = DateTime.UtcNow;
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

    public async Task CheckChallengeInstanceTimeout(CancellationToken cancellationToken)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(ChallengeNamespaceLabelSelector),
            cancellationToken: cancellationToken);

        var maxAge = DateTime.UtcNow.Subtract(infraConfig.ChallengeInstanceTimeout);
        foreach (var ns in nsList.Items.Where(n => n.Metadata.CreationTimestamp < maxAge))
        {
            var playerId = Guid.Parse(ns.GetLabel(PlayerIdLabel));
            var challengeName = ns.GetLabel(ChallengeLabel);
            var instanceId = Guid.Parse(ns.GetLabel(InstanceIdLabel));

            logger.LogInformation("Removing instance {InstanceId} by player {PlayerId} because it reached the instance timeout", ns.Name(), playerId);
            await kubernetes.DeleteNamespaceAsync(ns.Name(), cancellationToken: cancellationToken);

            var _ = mediator.Publish(new InstanceChangeNotification
            {
                PlayerId = playerId,
                Instance = new Instance {
                    Id = instanceId,
                    ChallengeName = challengeName,
                    InstanceState = InstanceState.Terminating
                },
            }, CancellationToken.None);

            var dbInstance = dbContext.Instances.SingleOrDefault(i => i.Id == instanceId);
            if (dbInstance != null)
            {
                dbInstance.TerminatedAt = DateTime.UtcNow;
                dbInstance.TerminationReason = Db.InstanceTerminationReason.Timeout;
                dbContext.SaveChanges();
            }
            else
            {
                logger.LogError("Instance {InstanceId} was terminated due to timeout but did not have a corresponding database instance entry.", instanceId);
            }
        }
    }

    public async Task<List<Instance>> GetChallengeInstances(CancellationToken cancellationToken)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(ChallengeNamespaceLabelSelector),
            cancellationToken: cancellationToken);

        var playersWithActiveInstances = nsList.Items.Select(ns => Guid.Parse(ns.GetLabel(PlayerIdLabel)));
        var instances = new List<Instance>();
        foreach(var playerId in playersWithActiveInstances)
        {
            instances.Add(await GetChallengeInstance(playerId, cancellationToken));
        }
        return instances;
    }

    public async Task<Instance> GetChallengeInstance(Guid playerId, CancellationToken cancellationToken)
    {
        using var activity = Constants.BergActivitySource.StartActivity();

        var labelSelector = new Dictionary<string, string>(ChallengeNamespaceLabelSelector)
        {
            { PlayerIdLabel, playerId.ToString() },
        };
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancellationToken);

        var ns = nsList.Items.FirstOrDefault();
        if (ns == null)
        {
            return new Instance
            {
                Id = null,
                ChallengeName = "",
                InstanceState = InstanceState.None
            };
        }

        var instanceId = Guid.Parse(ns.GetLabel(InstanceIdLabel));
        var challengeName = ns.GetLabel(ChallengeLabel);
        if (ns.Status.Phase == "Terminating")
            return new Instance
            {
                Id = instanceId,
                ChallengeName = challengeName,
                InstanceState = InstanceState.Terminating
            };

        var challenge = await _challengeClient.ReadNamespacedAsync<V1Challenge>(_bergNamespace, challengeName, cancellationToken);

        var podList = await kubernetes.ListNamespacedPodAsync(ns.Name(), cancellationToken: cancellationToken);
        if (podList.Items.Any(p => p.Status.Phase != "Running") || podList.Items.Count == 0)
        {
            return new Instance
            {
                Id = instanceId,
                ChallengeName = challengeName,
                InstanceState = InstanceState.Starting
            };
        }

        var serviceList = await kubernetes.ListNamespacedServiceAsync(ns.Name(), cancellationToken: cancellationToken);
        var httpRouteList = await _httpRouteClient
            .ListNamespacedAsync<CustomResourceList<V1HTTPRoute>>(ns.Name(), cancellationToken);
        var tlsRouteList = await _tlsRouteClient
            .ListNamespacedAsync<CustomResourceList<V1Alpha2TLSRoute>>(ns.Name(), cancellationToken);

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
            Id = instanceId,
            ChallengeName = challengeName,
            InstanceState = InstanceState.Running,
            Services = services,
            Timeout = ns.CreationTimestamp()?.Add(infraConfig.ChallengeInstanceTimeout)
        };
    }

    public async Task<Instance> StartChallengeInstance(Guid playerId, V1Challenge challenge, CancellationToken cancellationToken)
    {
        var challengeName = challenge.Metadata.Name;
        var labelSelector = new Dictionary<string, string>(ChallengeNamespaceLabelSelector)
        {
            { PlayerIdLabel, playerId.ToString() }
        };
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancellationToken);
        if (nsList.Items.Any())
        {
            var existingInstance = await GetChallengeInstance(playerId, cancellationToken);
            logger.LogWarning("Player {PlayerId} tried to start challenge {NewChallengeName}, but already had an instance of challenge {OldChallengeName} running! ({InstanceId})", playerId, challengeName, existingInstance.ChallengeName, existingInstance.Id);
            return existingInstance;
        };

        if ((challenge.Spec.Containers?.Count ?? 0) == 0)
            throw new ArgumentException("Challenge can't be instantiated");

        var instanceId = UUIDNext.Uuid.NewSequential();
        string? dynamicFlag;
        if (challenge.Spec.SupportsDynamicFlags)
        {
            var entropy = RandomNumberGenerator.GetHexString(12, true);
            dynamicFlag = challenge.Spec.Flag.TrimEnd('}') + '_' + entropy + '}';
        }
        else
        {
            dynamicFlag = null;
        }

        dbContext.Instances.Add(new Db.Instance
        {
            Id = instanceId,
            PlayerId = playerId,
            StartedAt = DateTime.UtcNow,
            TerminatedAt = null,
            TerminationReason = null,
            ChallengeName = challenge.Metadata.Name,
            DynamicFlag = dynamicFlag,
        });
        dbContext.SaveChanges();

        var ns = await kubernetes.CreateNamespaceAsync(new V1Namespace
        {
            Metadata = new V1ObjectMeta
            {
                Name = $"challenge-{playerId}",
                Labels = new Dictionary<string, string>(ChallengeNamespaceLabelSelector)
                {
                    { ChallengeLabel, challengeName },
                    { InstanceIdLabel, instanceId.ToString() },
                    { PlayerIdLabel, playerId.ToString() },
                }
            }
        }, cancellationToken: cancellationToken);

        try
        {
            var imagePullSecret =
                await kubernetes.ReadNamespacedSecretAsync(infraConfig.PullSecretName, _bergNamespace, cancellationToken: cancellationToken);
            await kubernetes.CreateNamespacedSecretAsync(new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Name = infraConfig.PullSecretName,
                },
                Type = "kubernetes.io/dockerconfigjson",
                Data = imagePullSecret.Data
            }, ns.Name(), cancellationToken: cancellationToken);
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
            await _ciliumNetworkPolicyClient.CreateNamespacedAsync(networkPolicy, ns.Name(), cancellationToken);
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
                    await kubernetes.CreateNamespacedServiceAsync(service, ns.Name(), cancellationToken: cancellationToken);
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
                    await kubernetes.CreateNamespacedServiceAsync(service, ns.Name(), cancellationToken: cancellationToken);
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
                    await _httpRouteClient.CreateNamespacedAsync(httpRoute, ns.Name(), cancel: cancellationToken);
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
                    await _tlsRouteClient.CreateNamespacedAsync(tlsRoute, ns.Name(), cancel: cancellationToken);
                }
                catch (HttpOperationException ex)
                {
                    logger.LogError("Got exception while creating TLSRoute: {}", ex);
                    logger.LogError("Response.Content: {}", ex.Response.Content);
                    logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(tlsRoute));
                }
            }

            if (container.DynamicFlag?.Content != null)
            {
                var dynamicContent = container.DynamicFlag.Content;
                var dynamicFlagContent = new V1ConfigMap
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = "flag-content",
                        NamespaceProperty = ns.Name(),
                        Labels = new Dictionary<string, string>
                        {
                            { ManagedByLabel, "berg" },
                            { ComponentLabel, "flag-content" },
                        }
                    },
                    BinaryData = new Dictionary<string, byte[]>() {
                        { "content", Encoding.UTF8.GetBytes((dynamicFlag ?? "invalid{content-flag-error}") + '\n') }
                    }
                };
                try
                {
                    await kubernetes.CreateNamespacedConfigMapAsync(dynamicFlagContent, ns.Name(), cancellationToken: cancellationToken);
                }
                catch (HttpOperationException ex)
                {
                    logger.LogError("Got exception while creating ConfigMap: {}", ex);
                    logger.LogError("Response.Content: {}", ex.Response.Content);
                    logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(dynamicFlagContent));
                }
            }

            if (container.DynamicFlag?.Executable != null)
            {
                var executableContent = container.DynamicFlag.Executable;
                var dynamicFlagExecutable = new V1ConfigMap
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = "flag-executable",
                        NamespaceProperty = ns.Name(),
                        Labels = new Dictionary<string, string>
                        {
                            { ManagedByLabel, "berg" },
                            { ComponentLabel, "flag-executable" },
                        }
                    },
                    BinaryData = new Dictionary<string, byte[]>() {
                        { "executable", dynamicFlagExecutableService.GenerateExecutable(dynamicFlag ?? "invalid{executable-flag-error}") }
                    },
                };
                try
                {
                    await kubernetes.CreateNamespacedConfigMapAsync(dynamicFlagExecutable, ns.Name(), cancellationToken: cancellationToken);
                }
                catch (HttpOperationException ex)
                {
                    logger.LogError("Got exception while creating ConfigMap: {}", ex);
                    logger.LogError("Response.Content: {}", ex.Response.Content);
                    logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(dynamicFlagExecutable));
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
            if (container.DynamicFlag?.Env != null)
            {
                var dynEnv = container.DynamicFlag.Env;
                env.Add(new V1EnvVar(dynEnv.Name, dynamicFlag ?? "invalid{env-flag-error}"));
            }
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
                            AllowPrivilegeEscalation = true,
                            Capabilities = new V1Capabilities
                            {
                                Drop = [
                                    // Prevent root users from reading files that do not have the respective
                                    // file permissions.
                                    "DAC_OVERRIDE",
                                ]
                            }
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

            if (container.DynamicFlag?.Content != null)
            {
                var dynContent = container.DynamicFlag.Content;
                podSpec.Containers[0].VolumeMounts = [
                    new V1VolumeMount
                    {
                        Name = "content",
                        MountPath = dynContent.Path,
                        SubPath = Path.GetFileName(dynContent.Path),
                        ReadOnlyProperty = true,
                    }
                ];
                podSpec.Volumes = [
                    new V1Volume
                    {
                        Name = "content",
                        ConfigMap = new V1ConfigMapVolumeSource
                        {
                            Name = "flag-content",
                            Items = [
                                new V1KeyToPath
                                {
                                    Key = "content",
                                    Mode = dynContent.Mode,
                                    Path = Path.GetFileName(dynContent.Path)
                                }
                            ],
                        }
                    }
                ];
            }

            if (container.DynamicFlag?.Executable != null)
            {
                var dynExecutable = container.DynamicFlag.Executable;
                podSpec.Containers[0].VolumeMounts = [
                    new V1VolumeMount
                    {
                        Name = "executable",
                        MountPath = dynExecutable.Path,
                        SubPath = Path.GetFileName(dynExecutable.Path),
                        ReadOnlyProperty = true,
                    }
                ];
                podSpec.Volumes = [
                    new V1Volume
                    {
                        Name = "executable",
                        ConfigMap = new V1ConfigMapVolumeSource
                        {
                            Name = "flag-executable",
                            Items = [
                                new V1KeyToPath
                                {
                                    Key = "executable",
                                    Mode = dynExecutable.Mode,
                                    Path = Path.GetFileName(dynExecutable.Path)
                                }
                            ],
                        }
                    }
                ];
            }

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
                await kubernetes.CreateNamespacedDeploymentAsync(deployment, ns.Name(), cancellationToken: cancellationToken);
            }
            catch (HttpOperationException ex)
            {
                logger.LogError("Got exception while creating Pod: {}", ex);
                logger.LogError("Response.Content: {}", ex.Response.Content);
                logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(deployment));
            }
        }

        logger.LogInformation("Created instance of challenge: {}", challengeName);
        var instance = new Instance { Id = instanceId, ChallengeName = challengeName, InstanceState = InstanceState.Starting };

        var _ = mediator.Publish(new InstanceChangeNotification
        {
            PlayerId = playerId,
            Instance = instance,
        }, CancellationToken.None);

        return instance;
    }

    public async Task<Instance> StopChallengeInstance(Guid playerId, CancellationToken cancellationToken)
    {
        var labelSelector = new Dictionary<string, string>(ChallengeNamespaceLabelSelector)
        {
            { PlayerIdLabel, playerId.ToString() },
        };
        var nsList = await kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancellationToken);
        var ns = nsList.Items.FirstOrDefault();
        if (ns == null)
        {
            return new Instance
            {
                Id = null,
                ChallengeName = "",
                InstanceState = InstanceState.None
            };
        }

        var instanceId = Guid.Parse(ns.GetLabel(InstanceIdLabel));
        var challengeName = ns.GetLabel(ChallengeLabel);
        await kubernetes.DeleteNamespaceAsync(ns.Name(), gracePeriodSeconds: 0, cancellationToken: cancellationToken);
        logger.LogInformation("Terminated instance {InstanceId}", instanceId);

        var dbInstance = dbContext.Instances.SingleOrDefault(i => i.Id == instanceId);
        if (dbInstance != null)
        {
            dbInstance.TerminatedAt = DateTime.UtcNow;
            dbInstance.TerminationReason = Db.InstanceTerminationReason.UserRequest;
            dbContext.SaveChanges();
        }
        else
        {
            logger.LogError("Instance {InstanceId} was terminated due to user request but did not have a corresponding database instance entry.", instanceId);
        }

        return new Instance { Id = instanceId, ChallengeName = challengeName, InstanceState = InstanceState.Terminating };
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