using Berg.Db;
using Discord;
using Discord.Rest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Berg.DiscordIntegration;

public class NotificationWorker : BackgroundService
{
    private readonly ILogger<NotificationWorker> _logger;
    private readonly IServiceScopeFactory  _serviceScopeFactory;
    private readonly DiscordConfiguration _discordConfiguration;

    public NotificationWorker(
        ILogger<NotificationWorker> logger,
        IServiceScopeFactory  serviceScopeFactory,
        IOptions<DiscordConfiguration> discordConfiguration)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _discordConfiguration = discordConfiguration.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_discordConfiguration.BotToken))
        {
            _logger.LogError("No bot token configured, aborting.");
            return;
        }

        var latestCheckedDateTime = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1));
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = new DiscordRestClient();
                await client.LoginAsync(TokenType.Bot, _discordConfiguration.BotToken);

                var channel = await client.GetChannelAsync(_discordConfiguration.ActivityChannelId) as IMessageChannel;
                if (_discordConfiguration.ActivityChannelId == 0 || channel == null)
                {
                    _logger.LogError("No or invalid channel id configured, aborting.");
                    return;
                }

                using var scope = _serviceScopeFactory.CreateScope();
                
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    if(stoppingToken.IsCancellationRequested)
                        return;
                    
                    var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
                    var latestSolves = dbContext.Solves
                        .Include(s => s.Player)
                        .Include(s => s.Challenge)
                        .Where(s => s.SolvedAt > latestCheckedDateTime)
                        .OrderBy(s => s.SolvedAt)
                        .ToList();

                    foreach (var solve in latestSolves)
                    {
                        var user = await client.GetUserAsync(ulong.Parse(solve.Player.DiscordId));
                        var username = user == null ? solve.Player.Name : user.Mention;
                        await channel.SendMessageAsync($"{username} solved challenge `{solve.Challenge.Name}`");

                        if (latestCheckedDateTime <= solve.SolvedAt)
                            latestCheckedDateTime = solve.SolvedAt;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while trying to send a notification: {}", ex);
            }
        }
    }
}