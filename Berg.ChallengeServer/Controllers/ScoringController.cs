using System.Text.Json.Serialization;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using Berg.Shared;
using Discord;
using Discord.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DiscordConfig = Berg.ChallengeServer.Configuration.DiscordConfig;

namespace Berg.ChallengeServer.Controllers;

[ApiController]
public class ScoringController : ControllerBase
{
    private readonly ILogger<ScoringController> _logger;
    private readonly CtfConfig _ctfConfig;
    private readonly DiscordConfig _discordConfig;
    private readonly BergDbContext _dbContext;
    private readonly ChallengeService _challengeService;
    private readonly ScoringService _scoringService;
    private readonly PlayerService _playerService;
    private readonly object _submitFlagLock = new();

    public ScoringController(
        ILogger<ScoringController> logger,
        CtfConfig ctfConfig,
        DiscordConfig discordConfig,
        BergDbContext dbContext,
        ChallengeService challengeService,
        ScoringService scoringService,
        PlayerService playerService)
    {
        _logger = logger;
        _challengeService = challengeService;
        _ctfConfig = ctfConfig;
        _discordConfig = discordConfig;
        _dbContext = dbContext;
        _scoringService = scoringService;
        _playerService = playerService;
    }

    public class SubmitFlagRequest
    {
        [JsonPropertyName("challenge")] public string? Challenge { get; set; }

        [JsonPropertyName("flag")] public string? Flag { get; set; }
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Route("/api/v1/flag")]
    public SubmitFlagResult SubmitFlag([FromBody] SubmitFlagRequest flagRequest)
    {
        var challenge = flagRequest.Challenge;
        var flag = flagRequest.Flag;
        if (challenge == null || flag == null)
            throw new ArgumentException("Values can't be null");

        var now = DateTime.UtcNow;
        if (_ctfConfig.Start > now)
            throw new ArgumentException("CTF has not started yet");
        if (_ctfConfig.End < now)
            throw new ArgumentException("CTF has ended, no more flags accepted");

        lock (_submitFlagLock)
        {
            var playerId = _playerService.GetPlayer(User).Id;
            var player = _dbContext.Players
                .Include(p => p.Submissions)
                .Include(p => p.Solves)
                .Include(p => p.Team)
                .FirstOrDefault(p => p.Id == playerId);
            if (player == null)
                throw new ArgumentException("Invalid player");

            if (player.Solves.Any(s => s.ChallengeId == challenge))
                return SubmitFlagResult.AlreadySolved;

            if (_dbContext.Solves.Where(s => s.Player.TeamId == player.TeamId).Any(s => s.ChallengeId == challenge))
                return SubmitFlagResult.AlreadySolved;

            var yesterday = now.Subtract(TimeSpan.FromDays(1));
            var latestFailedSubmissions = player.Submissions.Where(s => yesterday < s.SubmittedAt).ToList();
            if (latestFailedSubmissions.Count > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerDay)
            {
                _logger.LogWarning("Player {} has reached the daily submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }

            var oneHourAgo = now.Subtract(TimeSpan.FromHours(1));
            var submissionCountHour = latestFailedSubmissions.Count(s => oneHourAgo < s.SubmittedAt);
            if (submissionCountHour > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerHour)
            {
                _logger.LogWarning("Player {} has reached the hourly submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }

            var oneMinuteAgo = now.Subtract(TimeSpan.FromMinutes(1));
            var submissionCountMinute = latestFailedSubmissions.Count(s => oneMinuteAgo < s.SubmittedAt);
            if (submissionCountMinute > _ctfConfig.RateLimits.MaxInvalidFlagSubmissionsPerMinute)
            {
                _logger.LogWarning("Player {} has reached the minute submission limit", playerId);
                return SubmitFlagResult.RateLimited;
            }

            var challengeConfig = _challengeService.GetChallengeConfig(challenge);
            if (challengeConfig == null)
                throw new ArgumentException("Invalid challenge");

            var dbChallenge = _dbContext.Challenges.FirstOrDefault(c => c.Name == challenge);
            if (dbChallenge == null)
                throw new ArgumentException("Invalid db challenge");

            if (challengeConfig.Spec.Flag != flag.Trim())
            {
                // Invalid submission
                _dbContext.Submissions.Add(new Submission
                {
                    Id = Guid.NewGuid(),
                    Challenge = dbChallenge,
                    SubmittedAt = now,
                    Player = player,
                });
                _dbContext.SaveChanges();
                _logger.LogInformation("Player {} submitted an invalid flag for challenge {}", playerId, challenge);
                return SubmitFlagResult.Incorrect;
            }

            var firstBlood = !_dbContext.Solves.Any(s => s.ChallengeId == challenge);

            // Its a valid solve!
            SendDiscordNotification(player.DiscordId, player.Name, player.Team?.Name, challenge, firstBlood)
                .Wait();

            _dbContext.Solves.Add(new Solve
            {
                Id = Guid.NewGuid(),
                Challenge = dbChallenge,
                SolvedAt = now,
                Player = player,
            });
            _dbContext.SaveChanges();
            _logger.LogInformation("Player {} has solved challenge {}", playerId, challenge);
            return SubmitFlagResult.Correct;
        }
    }

    private async Task SendDiscordNotification(
        string solverDiscordId,
        string solverDiscordUsername,
        string? solverTeamName,
        string solvedChallenge,
        bool firstBlood)
    {
        try
        {
            var client = new DiscordRestClient();
            await client.LoginAsync(TokenType.Bot, _discordConfig.BotToken);

            var channel = await client.GetChannelAsync(_discordConfig.NotificationChannelId) as IMessageChannel;
            if (_discordConfig.NotificationChannelId == 0 || channel == null)
            {
                _logger.LogError("No or invalid channel id configured, did not send notification.");
                return;
            }

            var guild = await client.GetGuildAsync(_discordConfig.NotificationGuildId);
            if (_discordConfig.NotificationGuildId == 0 || guild == null)
            {
                _logger.LogError("No or guild id configured, did not send notification.");
                return;
            }

            if (solverTeamName != null)
                solverTeamName = $" ({solverTeamName})";

            var user = await guild.GetUserAsync(ulong.Parse(solverDiscordId));
            var username = user == null ? solverDiscordUsername : user.Mention;
            if (firstBlood)
            {
                await channel.SendMessageAsync(
                    $"{username}{solverTeamName} got first blood on challenge `{solvedChallenge}` :drop_of_blood:");
            }
            else
            {
                await channel.SendMessageAsync(
                    $"{username}{solverTeamName} solved challenge `{solvedChallenge}` :triangular_flag_on_post:");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while trying to send a notification: {}", ex);
        }
    }
    
    [HttpGet]
    [Route("/api/v1/scoreboard/teams")]
    public List<TeamRanking> GetTeamScoreboard()
    {
        return _scoringService.GetTeamScoreboard();
    }
    
    [HttpGet]
    [Route("/api/v1/scoreboard/players")]
    public List<PlayerRanking> GetPlayerScoreboard()
    {
        return _scoringService.GetPlayerScoreboard();
    }
    
    [HttpGet]
    [Route("/api/v1/activity")]
    public List<ActivityEntry> GetActivity()
    {
        return _dbContext.Solves
            .Include(s => s.Player)
            .OrderBy(s => s.SolvedAt)
            .Select(s => new ActivityEntry
            {
                PlayerId = s.PlayerId,
                SolvedAt = s.SolvedAt,
                ChallengeName = s.ChallengeId,
                TeamId = s.Player.TeamId
            })
            .ToList();
    }
}