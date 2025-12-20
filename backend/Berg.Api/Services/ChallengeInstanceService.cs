// ChallengeInstanceService is here to perform operations on ChallengeInstances
// Specifically, this controller service should:
// - Build an initial cache of ChallengeInstances
// - Watch for changes to ChallengeInstances and update the internal state
// - Expose methods to interact with challenge instances from a high level perspective
// - Allow adding hooks for changes to challenge instances for other services / controllers

using System.Security.Cryptography;
using Berg.Api.CustomResources;
using Berg.Api.CustomResources.Berg;
using Berg.Api.Models;
using Berg.Api.Notifications;
using k8s;
using MediatR;

namespace Berg.Api.Services;

public interface IChallengeInstanceService
{
    Task<IEnumerable<V1ChallengeInstance>> GetChallengeInstances(CancellationToken cancellationToken);
    Task<V1ChallengeInstance?> GetChallengeInstance(Guid playerId, CancellationToken cancellationToken);
    Task<V1ChallengeInstance> CreateChallengeInstance(Guid playerId, V1Challenge challenge, CancellationToken cancellationToken);
    Task<V1ChallengeInstance> DeleteChallengeInstance(Guid playerId, CancellationToken cancellationToken);
}

public class ChallengeInstanceService(
    ILogger<ChallengeService> logger,
    BergMetrics metrics,
    Db.BergDbContext dbContext,
    Kubernetes kubernetes,
    KubernetesClientConfiguration kubernetesConfig,
    IMediator mediator) :
    IChallengeInstanceService
{

    private readonly GenericClient _challengeInstanceClient = CustomResource.CreateGenericClient<V1ChallengeInstance>(kubernetes, false);
    private readonly List<Action<WatchEventType, V1ChallengeInstance>> _hooks = new List<Action<WatchEventType, V1ChallengeInstance>>();

    public async Task<IEnumerable<V1ChallengeInstance>> GetChallengeInstances(CancellationToken cancellationToken)
    {
        return (await _challengeInstanceClient
            .ListAsync<CustomResourceList<V1ChallengeInstance>>(cancellationToken)).Items;
    }

    public async Task<V1ChallengeInstance?> GetChallengeInstance(Guid playerId, CancellationToken cancellationToken)
    {

        logger.LogDebug("looking for challenge instance for player {}", playerId);
        try
        {
            return await _challengeInstanceClient.ReadAsync<V1ChallengeInstance>(playerId.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("exception caught: {}", ex);
            return null;
        }
    }

    public async Task<V1ChallengeInstance> CreateChallengeInstance(Guid playerId, V1Challenge challenge, CancellationToken cancellationToken)
    {
        // Check if challenge instance exists. If it does, return existing instance
        var challengeInstance = await this.GetChallengeInstance(playerId, cancellationToken);
        if (challengeInstance != null)
        {
            logger.LogWarning("Player {PlayerId} tried to start challenge {NewChallengeName}, but already had an instance of challenge {OldChallengeName} running! ({InstanceId})", playerId, challenge.Metadata.Name, challengeInstance.Spec.ChallengeRef.Name, challengeInstance.Status.InstanceId);
            return challengeInstance;
        }

        if ((challenge.Spec.Containers?.Count ?? 0) == 0)
            throw new ArgumentException("Challenge can't be instantiated");

        // Create challenge instance for player
        // TODO: configurable challengeInstanceClass
        // TODO: custom timeout
        string? dynamicFlag = null;
        if (challenge.Spec.SupportsDynamicFlags)
        {
            if (challenge.Spec.DynamicFlagMode == V1DynamicFlagMode.Suffix)
            {
                var entropy = RandomNumberGenerator.GetHexString(12, true);
                dynamicFlag = challenge.Spec.Flag.TrimEnd('}') + '_' + entropy + '}';
            }
            else
            {
                // We want to avoid changing the flag to not comply with the format if possible.
                // This requires the challenge authors to use {} for the flag contents though, if not we just do the entire flag as a fallback.
                // We could *maybe* parse V1Challenge.FlagFormat, however the suffix mode does not do this either and this sounds like a pain to implement.
                var flagBodyStartIndex = challenge.Spec.Flag.IndexOf("{");
                if (flagBodyStartIndex == -1)
                {
                    flagBodyStartIndex = 0;
                }
                var flagBodyEndIndex = challenge.Spec.Flag.IndexOf("}", flagBodyStartIndex);
                if (flagBodyEndIndex == -1)
                {
                    flagBodyEndIndex = challenge.Spec.Flag.Length;
                }

                var substitionCharacters = new Dictionary<char, char>
                {
                    { 'a', '4' },
                    { 'e', '3' },
                    { 'g', '6' },
                    { 'i', '1' },
                    { 'l', '1' },
                    { 'o', '0' },
                    { 'r', '2' },
                    { 's', '5' },
                    { 't', '7' }
                };

                var flagBody = challenge.Spec.Flag[flagBodyStartIndex..flagBodyEndIndex];

                var leetifiedFlagBody = flagBody.ToCharArray().Select((sourceCharacter, index) =>
                    {
                        if (!substitionCharacters.TryGetValue(sourceCharacter, out char leetifiedCharacter))
                        {
                            return sourceCharacter;
                        }
                        // Yes this doesn't have much entropy compared to the suffix mode
                        // however this should not really matter here as this dynamic flag mode is just intended for tricking flag sharers to share their flag.
                        var shouldLeetify = RandomNumberGenerator.GetInt32(2) == 0;
                        if (!shouldLeetify)
                        {
                            return sourceCharacter;
                        }
                        return leetifiedCharacter;
                    });
                dynamicFlag = challenge.Spec.Flag.Remove(flagBodyStartIndex, flagBodyEndIndex - flagBodyStartIndex).Insert(flagBodyStartIndex, string.Concat(leetifiedFlagBody));
            }
        }
        else
        {
            dynamicFlag = null;
        }

        logger.LogInformation("Creating instance in namespace: {}", kubernetesConfig.Namespace);
        logger.LogInformation("Creating instance for player: {}", playerId);
        logger.LogInformation("Creating instance with flag: {}", dynamicFlag ?? challenge.Spec.Flag);
        logger.LogInformation("Creating instance of challenge: {}", challenge.Metadata.Name);
        challengeInstance = new V1ChallengeInstance
        {
            Metadata = new k8s.Models.V1ObjectMeta
            {
                Name = playerId.ToString(),
            },
            Spec = new V1ChallengeInstanceSpec
            {
                ChallengeRef = new V1ChallengeRef
                {
                    Name = challenge.Metadata.Name,
                    Namespace = kubernetesConfig.Namespace
                },
                OwnerId = playerId,
                Flag = dynamicFlag ?? challenge.Spec.Flag
            }
        };
        challengeInstance = await _challengeInstanceClient.CreateAsync(challengeInstance, cancellationToken);

        var instanceId = UUIDNext.Uuid.NewSequential();
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

        logger.LogInformation("Created instance of challenge: {}", challenge.Metadata.Name);
        var instance = new Instance { Id = instanceId, PlayerId = playerId, ChallengeName = challenge.Metadata.Name, InstanceState = InstanceState.Starting, StartedAt = challengeInstance.Metadata.CreationTimestamp };

        var _ = mediator.Publish(new InstanceChangeNotification
        {
            Instance = instance,
        }, CancellationToken.None);

        return challengeInstance;
    }

    public async Task<V1ChallengeInstance?> DeleteChallengeInstance(Guid playerId, CancellationToken cancellationToken)
    {

        var challengeInstance = await this.GetChallengeInstance(playerId, cancellationToken);
        if (challengeInstance == null)
        {
            return null;
        }

        return await _challengeInstanceClient.DeleteAsync<V1ChallengeInstance>(playerId.ToString(), cancellationToken);
    }
}
