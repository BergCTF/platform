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
                .ToDictionary(c => c.DiscordId, c => c);
            foreach (var entry in _playerCache)
            {
                _playerCache[entry.Key] = dbPlayers[entry.Key];
            }
        }
    }

    private void UpdatePlayerInfo(ClaimsPrincipal user)
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
        
            var player = dbContext.Players.FirstOrDefault(p => p.DiscordId == discordId);
            if (player != null)
            {
                if (player.Name == discordName)
                    return;
            
                // Handle discord username changes
                player.Name = discordName;
                dbContext.SaveChanges();
                _playerCache[discordId] = player;
                return;
            }

            var newPlayer = new Player
            {
                Id = Guid.NewGuid(),
                DiscordId = discordId,
                Name = discordName
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
        UpdatePlayerInfo(user);
        return _playerCache[discordId];
    }
}