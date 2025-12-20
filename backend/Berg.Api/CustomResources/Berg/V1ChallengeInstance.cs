using System.Text.Json.Serialization;

namespace Berg.Api.CustomResources.Berg;

public class V1ChallengeInstance : CustomResource<V1ChallengeInstanceSpec, V1ChallengeInstanceStatus>
{
    public V1ChallengeInstance() : base(
        "ChallengeInstance",
        "challengeinstances",
        "berg.norelect.ch",
        "v1")
    {
    }
}

public class V1ChallengeInstanceSpec
{
    [JsonPropertyName("challengeRef")]
    public required V1ChallengeRef ChallengeRef { get; set; }

    [JsonPropertyName("flag")]
    public required string Flag { get; set; }

    [JsonPropertyName("instanceClass")]
    public string? InstanceClass { get; set; }

    [JsonPropertyName("ownerId")]
    public required Guid OwnerId { get; set; }

    [JsonPropertyName("terminationReason")]
    public V1TerminationReason? TerminationReason { get; set; }

    [JsonPropertyName("timeout")]
    public string? Timeout { get; set; }
}

public class V1ChallengeInstanceStatus
{
    [JsonPropertyName("conditions")]
    public List<V1ChallengeInstanceStatusCondition>? Conditions { get; set; }
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }
    [JsonPropertyName("instanceId")]
    public Guid? InstanceId { get; set; }
    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }
    [JsonPropertyName("observedGeneration")]
    public int? ObservedGeneration { get; set; }
    [JsonPropertyName("phase")]
    public V1ChallengeInstancePhase? Phase { get; set; }
    [JsonPropertyName("readyAt")]
    public DateTime? ReadyAt { get; set; }
    [JsonPropertyName("services")]
    public List<V1ChallengeInstanceStatusService>? Services { get; set; }
    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }
    [JsonPropertyName("terminatedAt")]
    public DateTime? TerminatedAt { get; set; }
}

public class V1ChallengeRef
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("namespace")]
    public required string Namespace { get; set; }
}

public enum V1TerminationReason
{
    UserRequest,
    Timeout,
    AdminTermination
}

public class V1ChallengeInstanceStatusCondition
{
    [JsonPropertyName("lastTransitionTime")]
    public DateTime? LastTransitionTime { get; set; }
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
    [JsonPropertyName("status")]
    public required V1ChallengeInstanceStatusConditionStatus Status { get; set; }
    [JsonPropertyName("type")]
    public required string Type { get; set; }
}

public class V1ChallengeInstanceStatusService
{
    [JsonPropertyName("appProtocol")]
    public string? AppProtocol { get; set; }
    [JsonPropertyName("hostname")]
    public required string Hostname { get; set; }
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("port")]
    public required int Port { get; set; }
    [JsonPropertyName("protocol")]
    public required string Protocol { get; set; }
    [JsonPropertyName("tls")]
    public bool? Tls { get; set; }
}

public enum V1ChallengeInstanceStatusConditionStatus
{
    True,
    False,
    Unknown
}

public enum V1ChallengeInstancePhase
{
    Pending,
    Creating,
    Starting,
    Running,
    Terminating,
    Terminated,
    Failed,
}
