using System.Text.Json.Serialization;
using Berg.Api.CustomResources.Berg;

namespace Berg.Api.Models;

public class Instance
{
    [JsonPropertyName("id")]
    public required Guid? Id { get; set; }

    [JsonPropertyName("playerId")]
    public required Guid? PlayerId { get; set; }

    [JsonPropertyName("name")]
    public required string ChallengeName { get; set; } = "";

    [JsonPropertyName("status")]
    public InstanceState InstanceState { get; set; } = InstanceState.None;

    [JsonPropertyName("services")]
    public List<Service> Services { get; set; } = [];

    [JsonPropertyName("timeout")]
    public DateTime? Timeout { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("terminatedAt")]
    public DateTime? TerminatedAt { get; set; }

    public static Instance FromCR(V1ChallengeInstance cr)
    {
        return new Instance
        {
            ChallengeName = cr.Spec.ChallengeRef.Name,
            PlayerId = cr.Spec.OwnerId,
            Id = cr.Status?.InstanceId,
            InstanceState = Instance.ToInstanceState(cr.Status?.Phase),
            Services = cr.Status?.Services?.Select(s => Instance.ToService(s)).ToList() ?? [],

            // TODO: other fields
        };
    }

    private static InstanceState ToInstanceState(V1ChallengeInstancePhase? phase) => phase switch
    {
        V1ChallengeInstancePhase.Pending => InstanceState.Starting,
        V1ChallengeInstancePhase.Creating => InstanceState.Starting,
        V1ChallengeInstancePhase.Starting => InstanceState.Starting,
        V1ChallengeInstancePhase.Running => InstanceState.Running,
        V1ChallengeInstancePhase.Terminating => InstanceState.Terminating,
        V1ChallengeInstancePhase.Terminated => InstanceState.None,
        V1ChallengeInstancePhase.Failed => InstanceState.None,
        // default to Starting if nothing is set, controller is not running or hasn't seen the challenge yet
        _ => InstanceState.Starting
    };

    private static Service ToService(V1ChallengeInstanceStatusService service)
    {
        return new Service
        {
            Name = service.Name,
            Hostname = service.Hostname,
            Port = service.Port,
            Protocol = service.Protocol.ToLower(),
            AppProtocol = service.AppProtocol?.ToLower() ?? "tcp",
            Tls = service.Tls ?? false
        };
    }
}

public enum InstanceState
{
    None,
    Starting,
    Running,
    Terminating,
}

public class Service
{
    [JsonPropertyName("name")]
    public string? Name { get; set; } = null;

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "";

    [JsonPropertyName("appProtocol")]
    public string AppProtocol { get; set; } = "";

    [JsonPropertyName("tls")]
    public bool Tls { get; set; } = false;
}


