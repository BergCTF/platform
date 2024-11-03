using Berg.Api.Notifications;
using Discord;
using Discord.Rest;
using MediatR;

namespace Berg.Api.Handlers;

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
        if (!string.IsNullOrEmpty(discordConfig.ClientId)) {
            var discordId = ulong.Parse(solve.PlayerFederatedId);
            var user = await guild.GetUserAsync(discordId);
            if (user != null) {
                // User is member of the discord server where the bot is configured to send messages
                username = user.Mention;
            }
        }

        await channel.SendMessageAsync(
                $"{username}{teamAddendum} has solved challenge `{solve.Challenge}` :triangular_flag_on_post:",
                    allowedMentions: allowedMentions);

        await client.LogoutAsync();
    }
}
