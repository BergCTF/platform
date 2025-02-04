using System.Diagnostics.Metrics;

namespace Berg.Api.Services;

public class BergMetrics
{
    private readonly Counter<int> _instancesStarted;
    private readonly UpDownCounter<int> _instancesCount;
    private readonly Counter<int> _invalidSubmissions;
    private readonly Counter<int> _validSubmissions;
    private readonly Counter<int> _rateLimitedSubmissions;
    private readonly UpDownCounter<int> _webSocketCount;
    private readonly Counter<int> _playersCreated;
    private readonly UpDownCounter<int> _playersCount;
    private readonly Counter<int> _teamsCreated;
    private readonly UpDownCounter<int> _teamsCount;

    public BergMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Berg.Api");
        _instancesStarted = meter.CreateCounter<int>("berg.instances.started", description: "The number of instances that were started overall");
        _instancesCount = meter.CreateUpDownCounter<int>("berg.instances.count", description: "The number of currently running instances");
        _validSubmissions = meter.CreateCounter<int>("berg.submissions.valid", description: "The number of valid flag submissions");
        _invalidSubmissions = meter.CreateCounter<int>("berg.submissions.invalid", description: "The number of invalid flag submissions");
        _rateLimitedSubmissions = meter.CreateCounter<int>("berg.submissions.ratelimited", description: "The number of flag submissions that were rate limited");
        _webSocketCount = meter.CreateUpDownCounter<int>("berg.websockets.count", description: "The number of websockets that are currently open");
        _playersCreated = meter.CreateCounter<int>("berg.players.created", description: "The number of players that were created overall");
        _playersCount = meter.CreateUpDownCounter<int>("berg.players.count", description: "The current number of players");
        _teamsCreated = meter.CreateCounter<int>("berg.teams.created", description: "The number of teams that were created overall");
        _teamsCount = meter.CreateUpDownCounter<int>("berg.teams.count", description: "The current number of teams");
    }

    public void InstanceStarted(string challengeName)
    {
        _instancesStarted.Add(1,
            new KeyValuePair<string, object?>("berg.challenge.name", challengeName));
        _instancesCount.Add(1);
    }

    public void InstanceStopped()
    {
        _instancesCount.Add(-1);
    }

    public void RateLimitedSubmission(string challengeName, Guid playerId)
    {
        _rateLimitedSubmissions.Add(1,
            new KeyValuePair<string, object?>("berg.player.id", playerId),
            new KeyValuePair<string, object?>("berg.challenge.name", challengeName));
    }

    public void InvalidSubmission(string challengeName, Guid playerId)
    {
        _invalidSubmissions.Add(1,
            new KeyValuePair<string, object?>("berg.player.id", playerId),
            new KeyValuePair<string, object?>("berg.challenge.name", challengeName));
    }

    public void ValidSubmission(string challengeName, Guid playerId)
    {
        _validSubmissions.Add(1,
            new KeyValuePair<string, object?>("berg.player.id", playerId),
            new KeyValuePair<string, object?>("berg.challenge.name", challengeName));
    }

    public void WebSocketStarted()
    {
        _webSocketCount.Add(1);
    }

    public void WebSocketStopped()
    {
        _webSocketCount.Add(-1);
    }

    public void PlayerCreated(Guid playerId)
    {
        _playersCreated.Add(1,
            new KeyValuePair<string, object?>("berg.player.id", playerId));
        _playersCount.Add(1);
    }

    public void PlayerDeleted()
    {
        _playersCount.Add(-1);
    }

    public void TeamCreated(Guid playerId)
    {
        _teamsCreated.Add(1,
            new KeyValuePair<string, object?>("berg.player.id", playerId));
        _teamsCount.Add(1);
    }

    public void TeamDeleted()
    {
        _teamsCount.Add(-1);
    }
}