namespace Berg.Api.Configuration;

public class InfraConfig
{
    public string ChallengeDomain { get; set; } = "localhost";
    public string PlatformDomain { get; set; } = "localhost";
    public string GatewayName { get; set; } = "";
    public string GatewayNamespace { get; set; } = "";
    public string PullSecretName { get; set; } = "";
    public string ChallengeImagePullPolicy { get; set; } = "Always";
    public int ChallengeHttpPort { get; set; } = 1337;
    public int ChallengeTlsPort { get; set; } = 31337;
    public string ChallengeHttpListenerName { get; set; } = "";
    public string ChallengeTlsListenerName { get; set; } = "";
    public Guid PlayerIdNamespace { get; set; } = Guid.Empty;
    public List<string>? RedirectUris { get; set; }
    public TimeSpan ChallengeInstanceTimeout { get; set; } = TimeSpan.FromHours(2);
    public string? ChallengeRuntimeClassName { get; set; }
    public string ChallengeEgressBandwidth { get; set; } = "1M";
    public string? HandoutServiceUrl { get; set; }
    public string? OpenTelemetryGrpcTracingEndpoint { get; set; }
    public string? OpenTelemetryGrpcMetricsEndpoint { get; set; }
    public string? OpenTelemetryGrpcLoggingEndpoint { get; set; }
    public bool UseKubernetesSecretKeyProvider { get; set; } = false;
}
