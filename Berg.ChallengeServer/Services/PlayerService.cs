using System.Security.Claims;
using Berg.ChallengeServer.Db;

namespace Berg.ChallengeServer.Services;

public class PlayerService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Dictionary<string, Player> _playerCache = new();
    private readonly object _playerUpdateLock = new();
    
    public PlayerService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public void RefreshPlayerInfo(BergDbContext dbContext)
    {
        lock (_playerUpdateLock)
        {
            var dbPlayers = dbContext.Players.ToList()
                .ToDictionary(p => p.DiscordId, p => p);
            foreach (var entry in dbPlayers)
            {
                _playerCache[entry.Key] = entry.Value;
            }
        }
    }

    private void CreatePlayer(ClaimsPrincipal user)
    {
        lock (_playerUpdateLock)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();

            var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (discordId == null)
                throw new ArgumentException("NameIdentifier Claim missing in user identity.");
            var discordName = user.FindFirstValue(ClaimTypes.Name);
            if (discordName == null)
                throw new ArgumentException("Name Claim missing in user identity.");
            var email = user.FindFirstValue(ClaimTypes.Email);
            if (email == null)
                throw new ArgumentException("Email Claim missing in user identity.");

            var existingPlayer = dbContext.Players.FirstOrDefault(p => p.DiscordId == discordId);
            if (existingPlayer != null)
            {
                // Update properties on login
                existingPlayer.Name = discordName;
                existingPlayer.Email = email;
                dbContext.SaveChanges();
                _playerCache[discordId] = existingPlayer;
                return;
            }
            
            var newPlayer = new Player
            {
                Id = Guid.NewGuid(),
                DiscordId = discordId,
                Name = discordName,
                CreatedAt = DateTime.UtcNow,
                Email = email
            };
            dbContext.Players.Add(newPlayer);
            dbContext.SaveChanges();
            _playerCache[discordId] = newPlayer;
        }
    }

    public Player GetPlayer(ClaimsPrincipal user)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId == null)
            throw new ArgumentException("NameIdentifier Claim missing in user identity.");

        if (_playerCache.TryGetValue(discordId, out var player))
            return player;
        CreatePlayer(user);
        return _playerCache[discordId];
    }
}