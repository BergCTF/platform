using Berg.ChallengeServer.CustomResources;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using k8s.Models;

namespace Berg.ChallengeServer.Test.Mock;

public class MockChallengeService : IChallengeService
{
    public IEnumerable<V1Challenge> GetChallenges()
    {
        return new List<V1Challenge>
        {
            new()
            {
                Metadata = new V1ObjectMeta
                {
                    Name = BergDbContextFactory.Challenge1Id,
                    NamespaceProperty = "berg",
                },
                Spec = new V1ChallengeSpec
                {
                    Author = "Mock Author",
                    Categories = new List<string> { "misc" },
                    FlagFormat = "mock{...}",
                    Description = "Mock Description 1",
                    Difficulty = "easy",
                    Flag = "mock{flag-1}",
                    StaticValue = null,
                    HideUntil = null,
                    AllowOutboundTraffic = false,
                    Attachments = new List<V1ChallengeAttachment>
                    {
                        new()
                        {
                            FileName = "mock-challenge-1.tar.gz",
                            DownloadUrl = "/handouts/mock-challenge-1.tar.gz"
                        }
                    },
                    Containers = new List<V1ChallengeContainer>()
                }
            },
            new()
            {
                Metadata = new V1ObjectMeta
                {
                    Name = BergDbContextFactory.Challenge2Id,
                    NamespaceProperty = "berg",
                },
                Spec = new V1ChallengeSpec
                {
                    Author = "Mock Author",
                    Categories = new List<string> { "misc" },
                    FlagFormat = "mock{...}",
                    Description = "Mock Description 2",
                    Difficulty = "easy",
                    Flag = "mock{flag-2}",
                    StaticValue = null,
                    HideUntil = null,
                    AllowOutboundTraffic = false,
                    Attachments = new List<V1ChallengeAttachment>
                    {
                        new()
                        {
                            FileName = "mock-challenge-2.tar.gz",
                            DownloadUrl = "/handouts/mock-challenge-2.tar.gz"
                        }
                    },
                    Containers = new List<V1ChallengeContainer>()
                }
            },
            new()
            {
                Metadata = new V1ObjectMeta
                {
                    Name = BergDbContextFactory.Challenge3Id,
                    NamespaceProperty = "berg",
                },
                Spec = new V1ChallengeSpec
                {
                    Author = "Mock Author",
                    Categories = new List<string> { "misc" },
                    FlagFormat = "mock{...}",
                    Description = "Mock Description 3",
                    Difficulty = "easy",
                    Flag = "mock{flag-3}",
                    StaticValue = null,
                    HideUntil = null,
                    AllowOutboundTraffic = false,
                    Attachments = new List<V1ChallengeAttachment>
                    {
                        new()
                        {
                            FileName = "mock-challenge-3.tar.gz",
                            DownloadUrl = "/handouts/mock-challenge-3.tar.gz"
                        }
                    },
                    Containers = new List<V1ChallengeContainer>()
                }
            }
        };
    }

    public V1Challenge? GetChallengeConfig(string challengeName)
    {
        return GetChallenges().FirstOrDefault(c => c.Name() == challengeName);
    }

    public void RefreshChallenges(BergDbContext dbContext)
    {
        // Do nothing intentionally.
        // Normally this would sync the challenges defined in the custom resources into the database.
    }

    public Task CheckChallengeInstanceTimout(CancellationToken cancel)
    {
        // Do nothing intentionally.
        // Normally this would clean up challenges that are past their timeout
        return Task.CompletedTask;
    }

    public Task<ChallengeInstanceStatus> GetChallengeInstance(Guid playerId, CancellationToken cancel)
    {
        return Task.FromResult(new ChallengeInstanceStatus
        {
            InstanceState = ChallengeInstanceState.None
        });
    }

    public Task<ChallengeInstanceStatus> StartChallengeInstance(Guid playerId, string challenge, CancellationToken cancel)
    {
        return Task.FromResult(new ChallengeInstanceStatus
        {
            Name = "mock-challenge",
            InstanceState = ChallengeInstanceState.Starting,
            Services = new List<Service>
            {
                new()
                {
                    Name = "mock-service",
                    Hostname = "mock-berg.local",
                    Port = 1337,
                    Protocol = "tcp",
                    AppProtocol = "http",
                    VHost = false
                }
            },
            InstanceTimeout = DateTime.UtcNow.AddHours(1)
        });
    }

    public Task<ChallengeInstanceStatus> StopChallengeInstance(Guid playerId, CancellationToken cancel)
    {
        return Task.FromResult(new ChallengeInstanceStatus
        {
            Name = "mock-challenge",
            InstanceState = ChallengeInstanceState.Terminating,
            InstanceTimeout = DateTime.UtcNow.AddHours(1)
        });
    }
}