using Discord;
using Discord.Rest;
using MediatR;

namespace Berg.Api.Notifications.Handlers;

public class DiscordSolveNotificationHandler(
    ILogger<DiscordSolveNotificationHandler> logger,
    Configuration.DiscordConfig discordConfig) : INotificationHandler<SolveNotification>
{
    public async Task Handle(SolveNotification solve, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(discordConfig.BotToken))
        {
            logger.LogDebug("Skipping discord solve notification due to missing bot token.");
            return;
        }

        if (solve.IsFrozen)
        {
            logger.LogDebug("Skipping discord solve notification due to an active freeze.");
            return;
        }

        if (solve.IsAdmin)
        {
            logger.LogDebug("Skipping discord solve notification due to an admin solve.");
            return;
        }

        var client = new DiscordRestClient();
        await client.LoginAsync(TokenType.Bot, discordConfig.BotToken);

        if (await client.GetChannelAsync(discordConfig.NotificationChannelId) is not IMessageChannel channel)
        {
            logger.LogError("Invalid channel id configured, did not send solve notification.");
            return;
        }
        var guild = await client.GetGuildAsync(discordConfig.NotificationGuildId);
        if (guild == null)
        {
            logger.LogError("Invalid guild id configured, did not send solve notification.");
            return;
        }

        var teamAddendum = solve.TeamName != null ? $" ({Format.Sanitize(solve.TeamName)})" : "";

        var username = Format.Sanitize(solve.PlayerName);
        var allowedMentions = new AllowedMentions();
        await channel.SendMessageAsync(
                $"{username}{teamAddendum} has solved challenge `{solve.Challenge}` :triangular_flag_on_post:",
                    allowedMentions: allowedMentions);

        await client.LogoutAsync();
    }
}
