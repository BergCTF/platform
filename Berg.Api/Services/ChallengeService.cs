using System.Text;
using Berg.Api.Configuration;
using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
using Berg.Api.CustomResources.Cilium;
using Berg.Api.CustomResources.GatewayApi;
using Berg.Api.Db;
using Berg.Shared;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace Berg.Api.Services;

public interface IChallengeService
{
    IEnumerable<V1Challenge> GetChallenges();
    V1Challenge? GetChallengeConfig(string challengeName);
    void RefreshChallenges(BergDbContext dbContext);
    Task CheckChallengeInstanceTimeout(CancellationToken cancel);
    Task<ChallengeInstanceStatus> GetChallengeInstance(Guid playerId, CancellationToken cancel);
    Task<ChallengeInstanceStatus> StartChallengeInstance(Guid playerId, string challenge, CancellationToken cancel);
    Task<ChallengeInstanceStatus> StopChallengeInstance(Guid playerId, CancellationToken cancel);
}

public class ChallengeService : IChallengeService
{
    private const string ManagedByLabel      = "app.kubernetes.io/managed-by";
    private const string ComponentLabel      = "app.kubernetes.io/component";
    private const string PlayerIdLabel       = "berg.norelect.ch/player-id";
    private const string ChallengeLabel      = "berg.norelect.ch/challenge";
    private const string ContainerLabel      = "berg.norelect.ch/container";
    private const string HostnameLabel       = "berg.norelect.ch/hostname";
    private readonly ILogger<ChallengeService> _logger;
    private readonly Kubernetes _kubernetes;
    private readonly GenericClient _challengeClient;
    private readonly GenericClient _httpRouteClient;
    private readonly GenericClient _tlsRouteClient;
    private readonly GenericClient _ciliumNetworkPolicyClient;
    private readonly string _bergNamespace;
    private readonly CtfConfig _ctfConfig;
    private readonly InfraConfig _infraConfig;

    private readonly object _refreshLock = new();
    private Dictionary<string, V1Challenge> _challenges = new();

    public ChallengeService(
        ILogger<ChallengeService> logger,
        Kubernetes kubernetes,
        CtfConfig ctfConfig,
        InfraConfig infraConfig)
    {
        _logger = logger;
        _kubernetes = kubernetes;
        _challengeClient = CustomResource.CreateGenericClient<V1Challenge>(kubernetes, false);
        _httpRouteClient = CustomResource.CreateGenericClient<V1HTTPRoute>(kubernetes, false);
        _tlsRouteClient = CustomResource.CreateGenericClient<V1Alpha2TLSRoute>(kubernetes, false);
        _ciliumNetworkPolicyClient = CustomResource.CreateGenericClient<V2CiliumNetworkPolicy>(kubernetes, false);
        _ctfConfig = ctfConfig;
        _infraConfig = infraConfig;
        _bergNamespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }

    public void RefreshChallenges(BergDbContext dbContext)
    {
        lock (_refreshLock)
        {
            var challengeList = _challengeClient
                .ListNamespacedAsync<CustomResourceList<V1Challenge>>(_bergNamespace).Result;

            _challenges = challengeList.Items
                .ToDictionary(c => c.Name(), c => c);

            var dbChallenges = dbContext.Challenges.ToList();
            var missingChallengeNames = _challenges.Values.Select(c => c.Name()).ToHashSet();
            missingChallengeNames.ExceptWith(dbChallenges.Select(c => c.Name));

            foreach (var missingChallengeName in missingChallengeNames)
            {
                dbContext.Challenges.Add(new Db.Challenge { Name = missingChallengeName });
            }

            dbContext.SaveChanges();
        }
    }

    public IEnumerable<V1Challenge> GetChallenges()
    {
        var now = DateTime.UtcNow;
        return _challenges.Values
            .Where(c => c.Spec.HideUntil == null || c.Spec.HideUntil <= now)
            .ToList();
    }

    public V1Challenge? GetChallengeConfig(string challengeName)
    {
        var now = DateTime.UtcNow;
        return _challenges.Values
            .Where(c => c.Spec.HideUntil == null || c.Spec.HideUntil <= now)
            .FirstOrDefault(c => c.Name() == challengeName);
    }

    public async Task CheckChallengeInstanceTimeout(CancellationToken cancel)
    {
        var labelSelector = new Dictionary<string, string>
        {
            { ManagedByLabel, "berg" },
            { ComponentLabel, "challenge" },
        };
        var nsList = await _kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);

        var maxAge = DateTime.UtcNow.Subtract(_infraConfig.ChallengeInstanceTimeout);
        foreach (var ns in nsList.Items.Where(n => n.Metadata.CreationTimestamp < maxAge))
        {
            _logger.LogInformation("Removing {} because it reached the instance timeout", ns.Name());
            await _kubernetes.DeleteNamespaceAsync(ns.Name(), cancellationToken: cancel);
        }
    }

    public async Task<ChallengeInstanceStatus> GetChallengeInstance(Guid playerId, CancellationToken cancel)
    {
        var now = DateTime.UtcNow;
        if (_ctfConfig.Start > now)
            return new ChallengeInstanceStatus();

        var labelSelector = new Dictionary<string, string>
        {
            { ManagedByLabel, "berg" },
            { ComponentLabel, "challenge" },
            { PlayerIdLabel, playerId.ToString() },
        };
        var nsList = await _kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);

        var ns = nsList.Items.FirstOrDefault();
        if (ns == null)
            return new ChallengeInstanceStatus { InstanceState = ChallengeInstanceState.None };

        var challengeName = ns.GetLabel(ChallengeLabel);
        if (ns.Status.Phase == "Terminating")
            return new ChallengeInstanceStatus
            {
                Name = challengeName,
                InstanceState = ChallengeInstanceState.Terminating
            };

        var challenge = await _challengeClient.ReadNamespacedAsync<V1Challenge>(_bergNamespace, challengeName, cancel);

        var podList = await _kubernetes.ListNamespacedPodAsync(ns.Name(), cancellationToken: cancel);
        if (podList.Items.Any(p => p.Status.Phase != "Running"))
           return new ChallengeInstanceStatus
           {
               Name = challengeName,
               InstanceState = ChallengeInstanceState.Starting
           };

        var serviceList = await _kubernetes.ListNamespacedServiceAsync(ns.Name(), cancellationToken: cancel);
        var httpRouteList = await _httpRouteClient
            .ListNamespacedAsync<CustomResourceList<V1HTTPRoute>>(ns.Name(), cancel);
        var tlsRouteList = await _tlsRouteClient
            .ListNamespacedAsync<CustomResourceList<V1Alpha2TLSRoute>>(ns.Name(), cancel);

        var services = new List<Service>();
        foreach (var container in challenge.Spec.Containers ?? new List<V1ChallengeContainer>())
        {
            foreach (var port in container.Ports ?? new List<V1ChallengePort>())
            {
                if (port.Type == V1ChallengePortType.InternalPort)
                    continue;
                var service = new Service
                {
                    Name = port.Name,
                    Hostname = _infraConfig.ChallengeDomain,
                    AppProtocol = port.AppProtocol,
                    Protocol = port.Protocol,
                    VHost = false,
                };

                if (port.Type == V1ChallengePortType.PublicPort)
                {
                    var serviceName = $"{container.Hostname}-node-port";
                    var infraService = serviceList.Items.FirstOrDefault(s => s.Name() == serviceName);
                    var infraPort = infraService?.Spec.Ports.FirstOrDefault(p => p.Port == port.Port);
                    service.Port = infraPort?.NodePort ?? 0;
                }
                else if (port.Type == V1ChallengePortType.PublicHttpRoute)
                {
                    var ingress = httpRouteList.Items
                        .FirstOrDefault(i => i.Name() == $"{container.Hostname}-{port.Port}");
                    service.Hostname = (ingress?.GetLabel(HostnameLabel) ?? "<loading>") + "." + _infraConfig.ChallengeDomain;
                    service.Port = _infraConfig.ChallengeHttpPort;
                    service.VHost = true;
                }
                else if (port.Type == V1ChallengePortType.PublicTlsRoute)
                {
                    var ingress = tlsRouteList.Items
                        .FirstOrDefault(i => i.Name() == $"{container.Hostname}-{port.Port}");
                    service.Hostname = (ingress?.GetLabel(HostnameLabel) ?? "<loading>") + "." +_infraConfig.ChallengeDomain;
                    service.Port = _infraConfig.ChallengeTlsPort;
                    service.VHost = true;
                }
                services.Add(service);
            }
        }

        return new ChallengeInstanceStatus
        {
            Name = challengeName,
            InstanceState = ChallengeInstanceState.Running,
            Services = services,
            InstanceTimeout = ns.CreationTimestamp()?.Add(_infraConfig.ChallengeInstanceTimeout)
        };
    }

    public async Task<ChallengeInstanceStatus> StartChallengeInstance(Guid playerId, string challenge,
        CancellationToken cancel)
    {
        var now = DateTime.UtcNow;
        if (_ctfConfig.Start > now)
            return new ChallengeInstanceStatus();

        var labelSelector = new Dictionary<string, string>
        {
            { ManagedByLabel, "berg" },
            { ComponentLabel, "challenge" },
            { PlayerIdLabel, playerId.ToString() }
        };
        var nsList = await _kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);
        if (nsList.Items.Any())
            throw new ArgumentException("A challenge is already running!");

        var challengeConfig = await _challengeClient.ReadNamespacedAsync<V1Challenge>(_bergNamespace, challenge, cancel);
        if (challengeConfig == null)
            throw new ArgumentException("Invalid challenge!");
        if(challengeConfig.Spec.HideUntil != null && DateTime.UtcNow < challengeConfig.Spec.HideUntil)
            throw new ArgumentException("Invalid challenge!");

        if ((challengeConfig.Spec.Containers?.Count ?? 0) == 0)
            throw new ArgumentException("Challenge can't be instantiated");

        var ns = await _kubernetes.CreateNamespaceAsync(new V1Namespace
        {
            Metadata = new V1ObjectMeta
            {
                Name = $"challenge-{playerId}",
                Labels = new Dictionary<string, string>
                {
                    { ManagedByLabel, "berg" },
                    { ComponentLabel, "challenge" },
                    { ChallengeLabel, challenge },
                    { PlayerIdLabel, playerId.ToString() },
                }
            }
        }, cancellationToken: cancel);

        try
        {
            var imagePullSecret =
                await _kubernetes.ReadNamespacedSecretAsync(_infraConfig.PullSecretName, _bergNamespace, cancellationToken: cancel);
            await _kubernetes.CreateNamespacedSecretAsync(new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Name = _infraConfig.PullSecretName,
                },
                Type = "kubernetes.io/dockerconfigjson",
                Data = imagePullSecret.Data
            }, ns.Name(), cancellationToken: cancel);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogWarning("Image pull secret '{}' not found in namespace '{}'", _infraConfig.PullSecretName, _bergNamespace);
            _logger.LogWarning("Detailed exception for pull secret copy operations: {}", ex);
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
                Egress = new List<V2CiliumEgressRule>
                {
                    new()
                    {
                        ToEndpoints = new List<V1LabelSelector>
                        {
                            new()
                            {
                                MatchLabels = new Dictionary<string, string>
                                {
                                    { "k8s:io.kubernetes.pod.namespace", "kube-system" },
                                    { "k8s:k8s-app", "kube-dns" },
                                }
                            }
                        },
                        ToPorts = new List<V2CiliumPortRule>
                        {
                              new()
                              {
                                  Ports = new List<V2CiliumPortProtocol>
                                  {
                                      new()
                                      {
                                          Port = "53"
                                      }
                                  },
                                  Rules = challengeConfig.Spec.AllowOutboundTraffic ? null : new V2CiliumL7Rule
                                  {
                                      Dns = new List<V2CiliumPortRuleDns>
                                      {
                                          new()
                                          {
                                              // If outbound traffic is forbidden, only accept
                                              // dns requests for internal services
                                              MatchPattern = $"*.{ns.Name()}.svc.cluster.local.",
                                          }
                                      }
                                  }
                              }
                        }
                    },
                    new()
                    {
                        ToEndpoints = new List<V1LabelSelector>
                        {
                            // Allow traffic to other pods in the same namespace
                            // by matching all labels
                            new()
                        }
                    },
                    new()
                    {
                        // Allow outbound traffic to self using the public ip address
                        // as this is required for OIDC to work if an IDP is deployed within
                        // a challenge.
                        ToEntities = new List<string> { CiliumEntity.Host },
                        ToPorts = new List<V2CiliumPortRule>
                        {
                            new()
                            {
                                Ports = new List<V2CiliumPortProtocol>
                                {
                                    new()
                                    {
                                        Port = _infraConfig.ChallengeHttpPort.ToString()
                                    },
                                    new()
                                    {
                                        Port = _infraConfig.ChallengeTlsPort.ToString()
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        if (challengeConfig.Spec.AllowOutboundTraffic)
        {
            networkPolicy.Spec.Egress.Add(new V2CiliumEgressRule
            {
                ToEntities = new List<string>
                {
                    CiliumEntity.World
                }
            });
        }

        try
        {
            await _ciliumNetworkPolicyClient.CreateNamespacedAsync(networkPolicy, ns.Name(), cancel);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError("Got exception while creating CiliumNetworkPolicy: {}", ex);
            _logger.LogError("Response.Content: {}", ex.Response.Content);
            _logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(networkPolicy));
        }

        var serviceEndpoints = new Dictionary<string, string>();

        foreach (var container in challengeConfig.Spec.Containers ?? new List<V1ChallengeContainer>())
        {
            var allPorts = container.Ports ?? new List<V1ChallengePort>();
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
                    await _kubernetes.CreateNamespacedServiceAsync(service, ns.Name(), cancellationToken: cancel);
                }
                catch (HttpOperationException ex)
                {
                    _logger.LogError("Got exception while creating Service: {}", ex);
                    _logger.LogError("Response.Content: {}", ex.Response.Content);
                    _logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(service));
                }
            }

            var publicPorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.PublicPort)
                .ToList() ?? new List<V1ChallengePort>();
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
                    await _kubernetes.CreateNamespacedServiceAsync(service, ns.Name(), cancellationToken: cancel);
                }
                catch (HttpOperationException ex)
                {
                    _logger.LogError("Got exception while creating Service: {}", ex);
                    _logger.LogError("Response.Content: {}", ex.Response.Content);
                    _logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(service));
                }
            }

            var httpRoutePorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.PublicHttpRoute)
                .ToList() ?? new List<V1ChallengePort>();
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
                        Hostnames = new List<string>
                        {
                            $"{serviceGuid}.{_infraConfig.ChallengeDomain}"
                        },
                        ParentRefs = new List<V1ParentReference>
                        {
                            new()
                            {
                                Kind = "Gateway",
                                Name = _infraConfig.GatewayName,
                                SectionName = _infraConfig.ChallengeHttpListenerName,
                                Namespace = _bergNamespace,
                            }
                        },
                        Rules = new List<V1HTTPRouteRule>
                        {
                            new ()
                            {
                                BackendRefs = new List<V1HttpBackendRef>
                                {
                                    new ()
                                    {
                                        Name = container.Hostname,
                                        Port = httpRoutePort.Port,
                                        Namespace = ns.Name()
                                    }
                                }
                            }
                        }
                    }
                };
                if (httpRoutePort.Name != null)
                {
                    serviceEndpoints.Remove(httpRoutePort.Name);
                    serviceEndpoints.Add(httpRoutePort.Name,
                        $"{serviceGuid}.{_infraConfig.ChallengeDomain}:{_infraConfig.ChallengeHttpPort}");
                }
                try
                {
                    await _httpRouteClient.CreateNamespacedAsync(httpRoute, ns.Name(), cancel: cancel);
                }
                catch (HttpOperationException ex)
                {
                    _logger.LogError("Got exception while creating HttpRoute: {}", ex);
                    _logger.LogError("Response.Content: {}", ex.Response.Content);
                    _logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(httpRoute));
                }
            }

            var tlsRoutePorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.PublicTlsRoute)
                .ToList() ?? new List<V1ChallengePort>();
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
                        Hostnames = new List<string>
                        {
                            $"{serviceGuid}.{_infraConfig.ChallengeDomain}"
                        },
                        ParentRefs = new List<V1ParentReference>
                        {
                            new()
                            {
                                Kind = "Gateway",
                                Name = _infraConfig.GatewayName,
                                SectionName = _infraConfig.ChallengeTlsListenerName,
                                Namespace = _bergNamespace,
                            }
                        },
                        Rules = new List<V1Alpha2TLSRouteRule>
                        {
                            new ()
                            {
                                BackendRefs = new List<V1BackendRef>
                                {
                                    new ()
                                    {
                                        Name = container.Hostname,
                                        Port = tlsRoutePort.Port,
                                        Namespace = ns.Name()
                                    }
                                }
                            }
                        }
                    }
                };
                if (tlsRoutePort.Name != null)
                {
                    serviceEndpoints.Remove(tlsRoutePort.Name);
                    serviceEndpoints.Add(tlsRoutePort.Name,
                        $"{serviceGuid}.{_infraConfig.ChallengeDomain}:{_infraConfig.ChallengeTlsPort}");
                }
                try
                {
                    await _tlsRouteClient.CreateNamespacedAsync(tlsRoute, ns.Name(), cancel: cancel);
                }
                catch (HttpOperationException ex)
                {
                    _logger.LogError("Got exception while creating TLSRoute: {}", ex);
                    _logger.LogError("Response.Content: {}", ex.Response.Content);
                    _logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(tlsRoute));
                }
            }
        }

        foreach (var container in challengeConfig.Spec.Containers ?? new List<V1ChallengeContainer>())
        {
            var env = (container.Environment ?? new Dictionary<string, object>())
                .Select(e => new V1EnvVar(e.Key, e.Value.ToString()))
                .Concat(serviceEndpoints.Select(e => new V1EnvVar($"{e.Key.ToUpperInvariant()}_ENDPOINT", e.Value)))
                .ToList();
            env.Add(new V1EnvVar("CHALLENGE_NAMESPACE", ns.Name()));
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Name = container.Hostname,
                    Labels = new Dictionary<string, string>
                    {
                        { ManagedByLabel, "berg" },
                        { ComponentLabel, "challenge-container" },
                        { ChallengeLabel, challenge },
                        { PlayerIdLabel, playerId.ToString() },
                        { ContainerLabel, container.Hostname }
                    }
                },
                Spec = new V1PodSpec
                {
                    RestartPolicy = "Always",
                    Hostname = container.Hostname,
                    EnableServiceLinks = false,
                    AutomountServiceAccountToken = false,
                    TerminationGracePeriodSeconds = 0,
                    ImagePullSecrets = new List<V1LocalObjectReference> { new(_infraConfig.PullSecretName) },
                    Containers = new List<V1Container>
                    {
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
                    },
                }
            };
            try
            {
                await _kubernetes.CreateNamespacedPodAsync(pod, ns.Name(), cancellationToken: cancel);
            }
            catch (HttpOperationException ex)
            {
                _logger.LogError("Got exception while creating Pod: {}", ex);
                _logger.LogError("Response.Content: {}", ex.Response.Content);
                _logger.LogError("Object Details: \n{}", KubernetesYaml.Serialize(pod));
            }
        }

        _logger.LogInformation("Created instance of challenge: {}", challenge);
        return new ChallengeInstanceStatus { Name = challenge, InstanceState = ChallengeInstanceState.Starting };
    }

    public async Task<ChallengeInstanceStatus> StopChallengeInstance(Guid playerId, CancellationToken cancel)
    {
        var labelSelector = new Dictionary<string, string>
        {
            { ManagedByLabel, "berg" },
            { ComponentLabel, "challenge" },
            { PlayerIdLabel, playerId.ToString() },
        };
        var nsList = await _kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);
        var ns = nsList.Items.FirstOrDefault();
        if (ns == null)
            return new ChallengeInstanceStatus { InstanceState = ChallengeInstanceState.None };

        await _kubernetes.DeleteNamespaceAsync(ns.Name(), gracePeriodSeconds: 0, cancellationToken: cancel);
        var challengeName = ns.GetLabel(ChallengeLabel);
        _logger.LogInformation("Deleted instance of challenge: {}", challengeName);
        return new ChallengeInstanceStatus { Name = challengeName, InstanceState = ChallengeInstanceState.Terminating };
    }

    private static string ToLabelSelector(Dictionary<string, string> labelSelector)
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