namespace Berg.Api.Configuration;

public class InfraConfig
{
    public string ChallengeDomain { get; set; } = "localhost";
    public string GatewayName { get; set; } = "";
    public string PullSecretName { get; set; } = "";
    public int ChallengeHttpPort { get; set; } = 1337;
    public int ChallengeTlsPort { get; set; } = 31337;
    public string ChallengeHttpListenerName { get; set; } = "";
    public string ChallengeTlsListenerName { get; set; } = "";
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan ChallengeInstanceTimeout { get; set; } = TimeSpan.FromHours(2);
}
