using System.Text;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Db;
using Berg.Shared;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Challenge = Berg.Shared.Challenge;

namespace Berg.ChallengeServer.Services;

public class ChallengeService
{
    private const string ManagedByLabel      = "app.kubernetes.io/managed-by";
    private const string ComponentLabel      = "app.kubernetes.io/component";
    private const string PlayerIdLabel       = "berg.norelect.ch/player-id";
    private const string ChallengeLabel      = "berg.norelect.ch/challenge";
    private const string ContainerLabel      = "berg.norelect.ch/container";
    private const string ImagePullSecretName = "berg-pull-secret";

    private readonly ILogger<ChallengeService> _logger;
    private readonly Kubernetes _kubernetes;
    private readonly GenericClient _challengeClient;
    private readonly string _namespace;
    private readonly CtfConfig _ctfConfig;

    private readonly object _refreshLock = new();
    private Dictionary<string, V1Challenge> _challenges = new();
    
    public ChallengeService(
        ILogger<ChallengeService> logger,
        Kubernetes kubernetes, 
        CtfConfig ctfConfig)
    {
        _logger = logger;
        _kubernetes = kubernetes;
        _challengeClient = new GenericClient(kubernetes, "berg.norelect.ch", "v1", "challenges", false);
        _ctfConfig = ctfConfig;
        _namespace = Environment.GetEnvironmentVariable("BERG_NAMESPACE") ?? "default";
    }

    public void RefreshChallenges(BergDbContext dbContext)
    {
        lock (_refreshLock)
        {
            var challengeList = _challengeClient
                .ListNamespacedAsync<V1BergCustomResourceList<V1Challenge>>(_namespace).Result;

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

    public List<V1Challenge> GetChallenges()
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
    
    public async Task CheckChallengeInstanceTimout(CancellationToken cancel)
    {
        var labelSelector = new Dictionary<string, string>
        {
            { ManagedByLabel, "berg" },
            { ComponentLabel, "challenge" },
        };
        var nsList = await _kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);

        var maxAge = DateTime.UtcNow.Subtract(_ctfConfig.ChallengeInstanceTimeout);
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
        
        var challenge = await _challengeClient.ReadNamespacedAsync<V1Challenge>(_namespace, challengeName, cancel);

        var podList = await _kubernetes.ListNamespacedPodAsync(ns.Name(), cancellationToken: cancel);
        if (podList.Items.Any(p => p.Status.Phase != "Running"))
           return new ChallengeInstanceStatus
           {
               Name = challengeName,
               InstanceState = ChallengeInstanceState.Starting
           };
        
        var serviceList = await _kubernetes.ListNamespacedServiceAsync(ns.Name(), cancellationToken: cancel);
        var ingressList = await _kubernetes.ListNamespacedIngressAsync(ns.Name(), cancellationToken: cancel);
        
        var services = new List<Service>();
        foreach (var container in challenge.Spec.Containers ?? new List<V1ChallengeContainer>())
        {
            foreach (var port in container.Ports ?? new List<V1ChallengePort>())
            {
                if (port.Type == V1ChallengePortType.Internal)
                    continue;
                var service = new Service
                {
                    Hostname = _ctfConfig.ChallengeDomain,
                    AppProtocol = port.AppProtocol,
                    Protocol = port.Protocol,
                    Port = 443,
                    VHost = false,
                };

                if (port.Type == V1ChallengePortType.PublicPort)
                {
                    var serviceName = $"{container.Hostname}-node-port";
                    var infraService = serviceList.Items.FirstOrDefault(s => s.Name() == serviceName);
                    var infraPort = infraService?.Spec.Ports.FirstOrDefault(p => p.Port == port.Port);
                    service.Port = infraPort?.NodePort ?? 0;
                }
                else if (port.Type == V1ChallengePortType.PublicVHost)
                {
                    var ingress = ingressList.Items
                        .FirstOrDefault(i => i.Name() == $"vhost-{container.Hostname}-{port.Port}");
                    service.Hostname = ingress?.Spec.Rules.FirstOrDefault()?.Host ?? "localhost";
                    service.Port = 443;
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
            InstanceTimeout = ns.CreationTimestamp()?.Add(_ctfConfig.ChallengeInstanceTimeout)
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
            { PlayerIdLabel, playerId.ToString() },
        };
        var nsList = await _kubernetes.ListNamespaceAsync(labelSelector: ToLabelSelector(labelSelector),
            cancellationToken: cancel);
        if (nsList.Items.Any())
            throw new ArgumentException("A challenge is already running!");
        
        var challengeConfig = await _challengeClient.ReadNamespacedAsync<V1Challenge>(_namespace, challenge, cancel);
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
                await _kubernetes.ReadNamespacedSecretAsync(ImagePullSecretName, _namespace, cancellationToken: cancel);
            await _kubernetes.CreateNamespacedSecretAsync(new V1Secret
            {
                Metadata = new V1ObjectMeta
                {
                    Name = ImagePullSecretName,
                },
                Type = "kubernetes.io/dockerconfigjson",
                Data = imagePullSecret.Data
            }, ns.Name(), cancellationToken: cancel);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogWarning("Image pull secret '{}' not found in namespace '{}'", ImagePullSecretName, _namespace);
            _logger.LogWarning("Detailed exception for pull secret copy operations: {}", ex);
        }

        await _kubernetes.CreateNamespacedNetworkPolicyAsync(new V1NetworkPolicy
        {
            Metadata = new V1ObjectMeta
            {
                Name = "challenge-network-policy",
            },
            Spec = new V1NetworkPolicySpec
            {
                Egress = new List<V1NetworkPolicyEgressRule>
                {
                    new()
                    {
                        To = new List<V1NetworkPolicyPeer>
                        {
                            new()
                            {
                                NamespaceSelector = new V1LabelSelector
                                {
                                    MatchLabels = new Dictionary<string, string>
                                    {
                                        {"kubernetes.io/metadata.name", "kube-system"}
                                    }
                                },
                                PodSelector = new V1LabelSelector
                                {
                                    MatchLabels = new Dictionary<string, string>
                                    {
                                        {"k8s-app", "kube-dns"}
                                    }
                                }
                            }
                        },
                        Ports = new List<V1NetworkPolicyPort>
                        {
                            new()
                            {
                                Port = "53",
                                Protocol = "UDP",
                            },
                            new()
                            {
                                Port = "53",
                                Protocol = "TCP",
                            },
                        }
                    },
                    new()
                    {
                        To = new List<V1NetworkPolicyPeer>
                        {
                            new()
                            {
                                NamespaceSelector = new V1LabelSelector
                                {
                                    MatchLabels = new Dictionary<string, string>
                                    {
                                        { "kubernetes.io/metadata.name", ns.Name() }
                                    }
                                }
                            },
                            new()
                            {
                                IpBlock = new V1IPBlock
                                {
                                    Cidr = "0.0.0.0/0",
                                    Except = new List<string>
                                    {
                                        "10.0.0.0/8",
                                        "172.16.0.0/12",
                                        "192.168.0.0/16",
                                    }
                                }
                            }
                        }
                    }
                },
                PolicyTypes = new List<string> { "Egress" }
            }
        }, ns.Name(), cancellationToken: cancel);
        
        foreach (var container in challengeConfig.Spec.Containers ?? new List<V1ChallengeContainer>())
        {
            await _kubernetes.CreateNamespacedPodAsync(new V1Pod
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
                    ImagePullSecrets = new List<V1LocalObjectReference> { new(ImagePullSecretName) },
                    Containers = new List<V1Container>
                    {
                        new()
                        {
                            SecurityContext = new V1SecurityContext
                            {
                                Privileged = false,
                                AllowPrivilegeEscalation = false,
                            },
                            Name = container.Hostname,
                            Image = container.Image,
                            ImagePullPolicy = "IfNotPresent",
                            // Resources = new V1ResourceRequirements
                            // {
                            //     Limits = new Dictionary<string, ResourceQuantity>
                            //     {
                            //         { "cpu", new ResourceQuantity("100m") },
                            //         { "memory", new ResourceQuantity("500Mi") }
                            //     }
                            // },
                            Env = container.Environment?
                                .Select(e => new V1EnvVar(e.Key, e.Value))
                                .ToList(),
                            Ports = container.Ports?
                                .Select(p => new V1ContainerPort(p.Port, protocol: p.Protocol.ToUpperInvariant()))
                                .ToList(),
                        }
                    },
                }
            }, ns.Name(), cancellationToken: cancel);

            var internalPorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.Internal or V1ChallengePortType.PublicVHost)
                .ToList() ?? new List<V1ChallengePort>();
            if (internalPorts.Any())
            {
                await _kubernetes.CreateNamespacedServiceAsync(new V1Service
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
                        Ports = internalPorts.Select(p => new V1ServicePort
                        {
                            AppProtocol = p.AppProtocol,
                            Port = p.Port,
                            TargetPort = p.Port,
                            Protocol = p.Protocol.ToUpperInvariant(),
                        }).ToList(),
                    }
                }, ns.Name(), cancellationToken: cancel);
            }
            
            var publicPorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.PublicPort)
                .ToList() ?? new List<V1ChallengePort>();
            if (publicPorts.Any())
            {
                await _kubernetes.CreateNamespacedServiceAsync(new V1Service
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
                }, ns.Name(), cancellationToken: cancel);
            }
            
            var vhostPorts = container.Ports?
                .Where(p => p.Type is V1ChallengePortType.PublicVHost)
                .ToList() ?? new List<V1ChallengePort>();
            foreach (var vhostPort in vhostPorts)
            {
                var serviceGuid = Guid.NewGuid();
                await _kubernetes.CreateNamespacedIngressAsync(new V1Ingress
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = $"vhost-{container.Hostname}-{vhostPort.Port}"
                    },
                    Spec = new V1IngressSpec
                    {
                        Rules = new List<V1IngressRule>
                        {
                            new()
                            {
                                Host = $"{serviceGuid}.{_ctfConfig.ChallengeDomain}",
                                Http = new V1HTTPIngressRuleValue
                                {
                                    Paths = new List<V1HTTPIngressPath>
                                    {
                                        new ()
                                        {
                                            Path = "/",
                                            Backend = new V1IngressBackend
                                            {
                                                Service = new V1IngressServiceBackend
                                                {
                                                    Name = container.Hostname,
                                                    Port = new V1ServiceBackendPort
                                                    {
                                                        Number = vhostPort.Port
                                                    },
                                                }
                                            },
                                            PathType = "Prefix",
                                        }
                                    }
                                }
                            }
                        }
                    }
                }, ns.Name(), cancellationToken: cancel);
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