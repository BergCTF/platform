using k8s;
using k8s.Models;
using Microsoft.Extensions.Options;
using Berg.DTO;
using Berg.Options;

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
    
    private readonly ILogger<ChallengeService> _logger;
    private readonly Kubernetes _kubernetes;
    private readonly ChallengeOptions _options;

    public ChallengeService(ILogger<ChallengeService> logger, Kubernetes kubernetes, IOptions<ChallengeOptions> options)
    {
        _logger = logger;
        _kubernetes = kubernetes;
        _options = options.Value;
    }

    public async Task<List<Challenge>> GetChallenges(string userId, CancellationToken cancellationToken)
    {
        var allNamespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var namespaces = allNamespaces.Items
            .Where(n => n.Status.Phase == "Active")
            .Where(n => n.GetLabel(ManagedByKey) == BergManager)
            .Where(n => n.GetLabel(ChallengeTypeKey) == ChallengeTypeShared || n.GetLabel(UserIdKey) == userId);

        var runningChallenges = namespaces.Select(n =>
        {
            var challengeId = n.GetLabel(ChallengeIdKey);
            var challengeInfo = _options.Challenges!.First(c => c.Id == challengeId);
            var services = _kubernetes.ListNamespacedService(n.Name());
            return new Challenge()
            {
                Id = challengeId,
                Name = challengeInfo.Name,
                Description = challengeInfo.Description,
                Type = challengeInfo.Type == ChallengeTypeShared ? ChallengeType.Shared : ChallengeType.PrivateInstance,
                ExpiresAt = challengeInfo.Type == ChallengeTypeShared ? null : n.Metadata.CreationTimestamp!.Value.ToUniversalTime().AddMinutes(_options.PrivateInstanceTimeoutMinutes ?? 0),
                Status = ChallengeStatus.Running,
                Services = services.Items.Where(s => s.Name().EndsWith("-exposed"))
                    .SelectMany(s => s.Spec.Ports.Select(p =>
                    new Service()
                    {
                        Hostname = _options.Hostname ?? _kubernetes.BaseUri.Host,
                        Port = p.NodePort ?? -1,
                        Protocol = p.AppProtocol,
                    })).ToList(),
                AttachmentLinks = challengeInfo.AttachmentLinks,
            };
        });

        var stoppedChallenges = _options.Challenges!
            .Where(c => runningChallenges.All(r => r.Id != c.Id))
            .Select(c => new Challenge()
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Type = c.Type == ChallengeTypeShared ? ChallengeType.Shared : ChallengeType.PrivateInstance,
                Status = ChallengeStatus.Stopped,
                ExpiresAt = null,
                Services = new List<Service>(),
            });

        return runningChallenges.Concat(stoppedChallenges).ToList();
    }

    public async Task CreateSharedChallenges(CancellationToken cancellationToken)
    {
        var namespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var sharedNamespaces = namespaces.Items
            .Where(n => n.GetLabel(ManagedByKey) == BergManager)
            .Where(n => n.GetLabel(ChallengeTypeKey) == ChallengeTypeShared);

        var sharedChallengeInfos = _options.Challenges!
            .Where(c => c.Type == ChallengeTypeShared)
            .ToList();
        _logger.LogInformation("Creating {} shared challenges", sharedChallengeInfos.Count);
        
        // Delete all existing deployments
        foreach (var ns in sharedNamespaces)
        {
            await _kubernetes.DeleteNamespaceAsync(ns.Name(), gracePeriodSeconds: 0, cancellationToken: cancellationToken);
        }
        
        // Create new deployments
        foreach (var challengeInfo in sharedChallengeInfos)
        {
            _logger.LogInformation("Creating shared challenge: {}", challengeInfo.Id);
            var namespaceName = "challenge-shared-" + Guid.NewGuid();
            var ns = await CreateNamespace("", namespaceName, ChallengeTypeShared, challengeInfo.Id, cancellationToken);
            foreach (var container in challengeInfo.Containers!)
            {
                await CreateDeployment(container, ns.Name(), cancellationToken);
                await CreateService(container, ns.Name(), cancellationToken);
            }
        }
    }

    public async Task<Challenge?> CreatePrivateInstance(string userId, string challengeId, CancellationToken cancellationToken)
    {
        var challengeInfo = _options.Challenges!
            .Where(c => c.Type == ChallengeTypePrivate)
            .FirstOrDefault(c => c.Id == challengeId);

        if (challengeInfo == null)
        {
            _logger.LogWarning("Can't find challenge with id '{}' and private instance type", challengeId);
            return null;
        }

        var allNamespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var userNamespaces = allNamespaces.Items
            .Where(n => n.GetLabel(UserIdKey) == userId)
            .ToList();

        if (userNamespaces.Count > 0)
        {
            _logger.LogInformation("User already has a private instance, deleting the old one");
            foreach (var existingNamespace in userNamespaces)
            {
                await _kubernetes.DeleteNamespaceAsync(existingNamespace.Name(), gracePeriodSeconds: 0, cancellationToken: cancellationToken);
            }
        }
        
        _logger.LogInformation("Creating private instance challenge '{}'", challengeInfo.Name);
        
        var namespaceName = "challenge-private-" + Guid.NewGuid();
        var ns = await CreateNamespace(userId, namespaceName, ChallengeTypePrivate, challengeInfo.Id, cancellationToken);
        var services = new List<Service>();
        foreach (var container in challengeInfo.Containers!)
        {
            await CreateDeployment(container, ns.Name(), cancellationToken);
            var service = await CreateService(container, ns.Name(), cancellationToken);

            if (service == null)
                continue;
            
            services.AddRange(service.Spec.Ports
                .Select(port => new Service()
                {
                    Hostname = _options.Hostname ?? _kubernetes.BaseUri.Host,
                    Port = port.NodePort ?? -1,
                    Protocol = port.Protocol.ToLowerInvariant()
                }));
        }

        return new Challenge()
        {
            Id = challengeInfo.Id,
            Name = challengeInfo.Name,
            Description = challengeInfo.Description,
            Type = challengeInfo.Type == ChallengeTypeShared ? ChallengeType.Shared : ChallengeType.PrivateInstance,
            Services = services,
            AttachmentLinks = challengeInfo.AttachmentLinks,
        };
    }
    
    public async Task CleanupExpiredDemandedChallenges(CancellationToken cancellationToken)
    {
        var namespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var demandedNamespaces = namespaces.Items
            .Where(n => n.GetLabel(ManagedByKey) == BergManager)
            .Where(n => n.GetLabel(ChallengeTypeKey) == ChallengeTypePrivate);

        var timeout = _options.PrivateInstanceTimeoutMinutes ?? 5;
        foreach (var ns in demandedNamespaces)
        {
            var createdAt = ns.Metadata.CreationTimestamp!.Value.ToUniversalTime();
            if (createdAt.AddMinutes(timeout) >= DateTime.UtcNow)
                continue;
            
            _logger.LogInformation("Private instance too old, deleting: {}", ns.Name());
            await _kubernetes.DeleteNamespaceAsync(ns.Metadata.Name, cancellationToken: cancellationToken);
        }
    }

    private async Task<V1Namespace> CreateNamespace(string userId, string namespaceName, string type, string challengeId, CancellationToken cancellationToken)
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
                    {ChallengeIdKey, challengeId},
                    {UserIdKey, userId}
                }
            }
        }, cancellationToken: cancellationToken);
    }

    private async Task<V1Service?> CreateService(ContainerInfo container, string ns, CancellationToken cancellationToken)
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
                        Labels = new Dictionary<string, string>()
                        {
                            { AppNameSelectorKey, container.ContainerName },
                        }
                    },
                    Spec = new V1PodSpec
                    {
                        AutomountServiceAccountToken = false,
                        Containers = new List<V1Container>
                        {
                            new()
                            {
                                Name = container.ContainerName,
                                Image = container.Image,
                                Env = container.Environment
                                    .Select(e => new V1EnvVar(e.Key, e.Value))
                                    .ToList(),
                                Resources = new V1ResourceRequirements()
                                {
                                    Limits = new Dictionary<string, ResourceQuantity>()
                                    {
                                        { "cpu", new ResourceQuantity("100m") },
                                        { "memory", new ResourceQuantity("500Mi") }
                                    },
                                },
                                Ports = container.Ports
                                    .Select(p => new V1ContainerPort(p.Port, protocol: p.Protocol.ToUpperInvariant()))
                                    .ToList(),
                            }
                        },
                    }
                }
            }
        };
        await _kubernetes.CreateNamespacedDeploymentAsync(deployment, ns, cancellationToken: cancellationToken);
    }

    public async Task KillPrivateInstance(string userId, string challengeId, CancellationToken cancellationToken)
    {
        var allNamespaces = await _kubernetes.ListNamespaceAsync(cancellationToken: cancellationToken);
        var userNamespaces = allNamespaces.Items
            .Where(n => n.GetLabel(UserIdKey) == userId)
            .ToList();

        var namespaceToDelete = userNamespaces.FirstOrDefault(n => n.GetLabel(ChallengeIdKey) == challengeId);
        if (namespaceToDelete != null)
        {
            await _kubernetes.DeleteNamespaceAsync(namespaceToDelete.Name(), gracePeriodSeconds: 0, cancellationToken: cancellationToken);
        }
    }
}
