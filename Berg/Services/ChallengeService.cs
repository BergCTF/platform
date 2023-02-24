using k8s;
using k8s.Models;
using Berg.DTO;
using Berg.Configuration;
using Berg.Middleware;

namespace Berg.Services;

public class ChallengeService
{
    // Label keys
    private const string ManagedByKey = "managed-by";
    private const string ChallengeTypeKey = "challenge-type";
    private const string ChallengeIdKey = "challenge-id";
    private const string UserIdKey = "user-id";
    private const string AppNameSelectorKey = "app.kubernetes.io/name";

    // Label values
    private const string BergManager = "berg";
    private const string ChallengeTypeShared = "shared";
    private const string ChallengeTypePrivate = "private";
    private const string PullSecretName = "ctf-pull-secret";
    
    private readonly ILogger<ChallengeService> _logger;
    private readonly Kubernetes _kubernetes;
    private readonly CtfInfo _ctfInfo;
    private int _staticPort = 30000;

    public ChallengeService(ILogger<ChallengeService> logger, Kubernetes kubernetes, CtfInfo ctfInfo)
    {
        _logger = logger;
        _kubernetes = kubernetes;
        _ctfInfo = ctfInfo;
    }

    public async Task<Challenge> GetChallenge(CachedPlayer? player, Guid challengeId,
        CancellationToken cancellationToken)
    {
        var configChallenge = _ctfInfo.Challenges.FirstOrDefault(c => c.Id == challengeId) ??
                              throw new ArgumentException("Invalid challenge Id");

        var allNamespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);

        var filter = allNamespaces.Items
            .Where(n => n.GetLabel(ManagedByKey) == BergManager);
        if (player == null)
        {
            filter = filter.Where(n => n.GetLabel(ChallengeTypeKey) == ChallengeTypeShared);
        }
        else
        {
            filter = filter.Where(n => n.GetLabel(ChallengeTypeKey) == ChallengeTypeShared ||
                                       n.GetLabel(UserIdKey) == player.Id.ToString());
        }
        var challengeNamespace = filter
            .FirstOrDefault(n => Guid.Parse(n.GetLabel(ChallengeIdKey)) == challengeId);

        var dtoChallenge = new Challenge
        {
            Id = challengeId,
            Category = configChallenge.Category,
            Name = configChallenge.Name,
            Description = configChallenge.Description,
            Type = configChallenge.Type == ChallengeTypeShared ? ChallengeType.Shared : ChallengeType.PrivateInstance,
            Sponsor = _ctfInfo.Sponsors.GetValueOrDefault(configChallenge.Sponsor ?? ""),
            ExpiresAt = null,
            Status = ChallengeStatus.Stopped,
            Services = new List<Service>(),
            Attachments = configChallenge.Attachments,
        };

        // If challenge is not running, we can't find service details,
        // so we just return the config details
        if (challengeNamespace == null)
            return dtoChallenge;
        
        // If challenge is a private instance
        if (dtoChallenge.Type == ChallengeType.PrivateInstance)
        {
            dtoChallenge.ExpiresAt = challengeNamespace.Metadata.CreationTimestamp!.Value
                .ToUniversalTime().AddMinutes(_ctfInfo.PrivateInstanceTimeoutMinutes);
        }

        var services = await _kubernetes.ListNamespacedServiceAsync(challengeNamespace.Name(), cancellationToken: cancellationToken);
        dtoChallenge.Status = ChallengeStatus.Running;
        dtoChallenge.Services = services.Items.Where(s => s.Name().EndsWith("-exposed"))
            .SelectMany(s => s.Spec.Ports.Select(p =>
                new Service
                {
                    Hostname = _ctfInfo.ChallengeServerHostname,
                    Port = p.NodePort ?? -1,
                    Protocol = p.AppProtocol,
                })).ToList();
        return dtoChallenge;
    }

    public async Task<List<Challenge>> GetChallenges(CachedPlayer? player, CancellationToken cancellationToken)
    {
        var challenges = new List<Challenge>();
        foreach (var challengeId in _ctfInfo.Challenges.Select(c => c.Id))
        {
            challenges.Add(await GetChallenge(player, challengeId, cancellationToken));
        }
        return challenges;
    }

    public async Task CreateSharedChallenges(CancellationToken cancellationToken)
    {
        var namespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var sharedNamespaces = namespaces.Items
            .Where(n => n.GetLabel(ManagedByKey) == BergManager)
            .Where(n => n.GetLabel(ChallengeTypeKey) == ChallengeTypeShared);

        var sharedChallengeInfos = _ctfInfo.Challenges!
            .Where(c => c.Type == ChallengeTypeShared)
            .Where(c => c.Containers.Any())
            .ToList();
        _logger.LogInformation("Creating {} shared challenges", sharedChallengeInfos.Count);
        
        // Delete all existing deployments
        foreach (var ns in sharedNamespaces)
        {
            await EnsureNamespaceDeleted(ns.Name(), cancellationToken);
        }
        
        // Create new deployments
        foreach (var challengeInfo in sharedChallengeInfos)
        {
            _logger.LogInformation("Creating shared challenge: {}", challengeInfo.Id);
            var namespaceName = "challenge-shared-" + Guid.NewGuid();
            var ns = await CreateNamespace("", namespaceName, ChallengeTypeShared, challengeInfo.Id, cancellationToken);
            await CreatePullSecret(ns.Name());
            foreach (var container in challengeInfo.Containers!)
            {
                await CreateDeployment(container, ns.Name(), cancellationToken);
                await CreateService(container, ns.Name(), cancellationToken, true);
            }
            await CreateNetworkPolicy(ns.Name(), cancellationToken);
        }
    }

    public async Task CreatePrivateInstance(Guid userId, Guid challengeId, CancellationToken cancellationToken)
    {
        var challengeInfo = _ctfInfo.Challenges!
            .Where(c => c.Type == ChallengeTypePrivate)
            .FirstOrDefault(c => c.Id == challengeId);

        if (challengeInfo == null)
        {
            _logger.LogWarning("Can't find challenge with id '{}' and private instance type", challengeId);
            return;
        }

        var allNamespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var userNamespaces = allNamespaces.Items
            .Where(n => n.GetLabel(UserIdKey) == userId.ToString())
            .ToList();

        if (userNamespaces.Count > 0)
        {
            _logger.LogInformation("User already has a private instance, deleting the old one");
            foreach (var existingNamespace in userNamespaces)
            {
                await EnsureNamespaceDeleted(existingNamespace.Name(), cancellationToken);
            }
        }
        
        _logger.LogInformation("Creating private instance challenge '{}'", challengeInfo.Name);
        
        var namespaceName = "challenge-private-" + Guid.NewGuid();
        var ns = await CreateNamespace(userId.ToString(), namespaceName, ChallengeTypePrivate, challengeInfo.Id, cancellationToken);
        await CreatePullSecret(ns.Name());
        foreach (var container in challengeInfo.Containers!)
        {
            await CreateDeployment(container, ns.Name(), cancellationToken);
            await CreateService(container, ns.Name(), cancellationToken);
        }
        await CreateNetworkPolicy(ns.Name(), cancellationToken);
    }

    public async Task CleanupExpiredDemandedChallenges(CancellationToken cancellationToken)
    {
        var namespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var demandedNamespaces = namespaces.Items
            .Where(n => n.GetLabel(ManagedByKey) == BergManager)
            .Where(n => n.GetLabel(ChallengeTypeKey) == ChallengeTypePrivate);

        var timeout = _ctfInfo.PrivateInstanceTimeoutMinutes;
        foreach (var ns in demandedNamespaces)
        {
            var createdAt = ns.Metadata.CreationTimestamp!.Value.ToUniversalTime();
            if (createdAt.AddMinutes(timeout) >= DateTime.UtcNow)
                continue;
            
            _logger.LogInformation("Private instance too old, deleting: {}", ns.Name());
            await _kubernetes.DeleteNamespaceAsync(ns.Metadata.Name, cancellationToken: cancellationToken);
        }
    }

    private async Task<V1Namespace> CreateNamespace(string userId, string namespaceName, string type, Guid challengeId, CancellationToken cancellationToken)
    {
        return  await _kubernetes.CreateNamespaceAsync(new V1Namespace
        {
            Metadata = new V1ObjectMeta
            {
                Name = namespaceName,
                Labels = new Dictionary<string, string>()
                {
                    {ManagedByKey, BergManager},
                    {ChallengeTypeKey, type},
                    {ChallengeIdKey, challengeId.ToString()},
                    {UserIdKey, userId}
                }
            }
        }, cancellationToken: cancellationToken);
    }
    
    private async Task CreatePullSecret(string nsName)
    {
        if(_ctfInfo.ImagePullSecret == null)
            return;
        await _kubernetes.CreateNamespacedSecretAsync(new V1Secret
        {
            Metadata = new V1ObjectMeta
            {
                Name = PullSecretName,
            },
            Type = "kubernetes.io/dockerconfigjson",
            Data = new Dictionary<string, byte[]>
            {
                {".dockerconfigjson", Convert.FromBase64String(_ctfInfo.ImagePullSecret)}
            }
        }, nsName);
    }
    
    private async Task<V1NetworkPolicy> CreateNetworkPolicy(string ns, CancellationToken cancellationToken)
    {
        return await _kubernetes.CreateNamespacedNetworkPolicyAsync(new V1NetworkPolicy
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
                                        {"kubernetes.io/metadata.name", ns}
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
                PolicyTypes = new List<string> {"Egress"}
            }
        }, ns, cancellationToken: cancellationToken);
    }

    private async Task<V1Service?> CreateService(ContainerInfo container, string ns, CancellationToken cancellationToken, bool useStaticPort = false)
    {
        var privateService = new V1Service
        {
            Metadata = new V1ObjectMeta
            {
                Name = container.ContainerName,
                NamespaceProperty = ns
            },
            Spec = new V1ServiceSpec
            {
                Selector = new Dictionary<string, string>
                {
                    {AppNameSelectorKey, container.ContainerName}
                },
                Type = "ClusterIP",
                Ports = container.Ports.Where(p => !p.Exposed).Select(p => new V1ServicePort()
                {
                    AppProtocol = p.AppProtocol,
                    Port = p.Port,
                    TargetPort = p.Port,
                    Protocol = p.Protocol.ToUpperInvariant(),
                }).ToList(),
            }
        };
        if (container.Ports.Any(p => !p.Exposed))
        {
            await _kubernetes.CreateNamespacedServiceAsync(privateService, ns, cancellationToken: cancellationToken);
        }

        var publicService = new V1Service
        {
            Metadata = new V1ObjectMeta
            {
                Name = container.ContainerName + "-exposed",
                NamespaceProperty = ns,
            },
            Spec = new V1ServiceSpec
            {
                Selector = new Dictionary<string, string>
                {
                    { AppNameSelectorKey, container.ContainerName }
                },
                Type = "NodePort",
                // ExternalTrafficPolicy = "Local", // will preserve source ip, but not forward traffic to other nodes
                Ports = container.Ports.Where(p => p.Exposed).Select(p => new V1ServicePort()
                {
                    Port = p.Port,
                    TargetPort = p.Port,
                    Protocol = p.Protocol.ToUpperInvariant(),
                    NodePort = useStaticPort ? _staticPort++ : null,
                    AppProtocol = p.AppProtocol
                }).ToList(),
            }
        };
        if (container.Ports.Any(p => p.Exposed))
        {
            return await _kubernetes.CreateNamespacedServiceAsync(publicService, ns, cancellationToken: cancellationToken);
        }
        return null;
    }

    private async Task CreateDeployment(ContainerInfo container, string ns, CancellationToken cancellationToken)
    {
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta
            {
                Name = container.ContainerName,
                NamespaceProperty = ns,
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>()
                    {
                        { AppNameSelectorKey, container.ContainerName },
                    },
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = container.ContainerName,
                        NamespaceProperty = ns,
                        Labels = new Dictionary<string, string>
                        {
                            { AppNameSelectorKey, container.ContainerName },
                        }
                    },
                    Spec = new V1PodSpec
                    {
                        AutomountServiceAccountToken = false,
                        TerminationGracePeriodSeconds = 0,
                        ImagePullSecrets = _ctfInfo.ImagePullSecret != null ? new List<V1LocalObjectReference>
                        {
                            new(PullSecretName)  
                        } : new List<V1LocalObjectReference>(),
                        Containers = new List<V1Container>
                        {
                            new()
                            {
                                Name = container.ContainerName,
                                Image = container.Image,
                                Env = container.Environment
                                    .Select(e => new V1EnvVar(e.Key, e.Value))
                                    .ToList(),
                                SecurityContext = new V1SecurityContext
                                {
                                    Privileged = container.Privileged
                                },
                                Resources = new V1ResourceRequirements
                                {
                                    Limits = new Dictionary<string, ResourceQuantity>
                                    {
                                        { "cpu", new ResourceQuantity("100m") },
                                        { "memory", new ResourceQuantity("500Mi") }
                                    }
                                },
                                Ports = container.Ports
                                    .Select(p => new V1ContainerPort(p.Port, protocol: p.Protocol.ToUpperInvariant()))
                                    .ToList(),
                            }
                        }
                    }
                }
            }
        };
        await _kubernetes.CreateNamespacedDeploymentAsync(deployment, ns, cancellationToken: cancellationToken);
    }

    public async Task KillPrivateInstance(Guid userId, Guid challengeId, CancellationToken cancellationToken)
    {
        var allNamespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var userNamespaces = allNamespaces.Items
            .Where(n => n.GetLabel(UserIdKey) == userId.ToString())
            .ToList();

        var namespaceToDelete = userNamespaces.FirstOrDefault(n => Guid.Parse(n.GetLabel(ChallengeIdKey)) == challengeId);
        if (namespaceToDelete != null)
        {
            await EnsureNamespaceDeleted(namespaceToDelete.Name(), cancellationToken);
        }
    }

    private async Task EnsureNamespaceDeleted(string namespaceName, CancellationToken cancellationToken)
    {
        await _kubernetes.DeleteNamespaceAsync(namespaceName, gracePeriodSeconds: 0, cancellationToken: cancellationToken);
        while(true)
        {
            var namespaceList = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
            if (namespaceList.Items.All(n => n.Name() != namespaceName))
                break;
            await Task.Delay(250, cancellationToken);
        }
    }
}
