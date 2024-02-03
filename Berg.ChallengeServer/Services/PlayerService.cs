using System.Security.Claims;
using Berg.ChallengeServer.Db;
using Microsoft.EntityFrameworkCore;

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
            var dbPlayers = dbContext.Players
                .Include(p => p.Attributes).ToList()
                .ToDictionary(p => p.DiscordId, p => p);
            foreach (var entry in dbPlayers)
            {
                _playerCache[entry.Key] = entry.Value;
            }
        }
    }

    public Player GetPlayer(ClaimsPrincipal user)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId == null)
            throw new ArgumentException("NameIdentifier Claim missing in user identity.");
        var discordName = user.FindFirstValue(ClaimTypes.Name);
        if (discordName == null)
            throw new ArgumentException("Name Claim missing in user identity.");
        var email = user.FindFirstValue(ClaimTypes.Email);
        if (email == null)
            throw new ArgumentException("Email Claim missing in user identity.");
        
        lock (_playerUpdateLock)
        {
            if (_playerCache.TryGetValue(discordId, out var player))
            {
                if (player.Name != discordName || player.Email != email)
                    UpdatePlayer(discordId, discordName, email);
                return player;
            }
            CreatePlayer(discordId, discordName, email);
            return _playerCache[discordId];
        }
    }

    private void CreatePlayer(string discordId, string discordName, string email)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
        
        var newPlayer = new Player
        {
            Id = Guid.NewGuid(),
            DiscordId = discordId,
            Name = discordName,
            CreatedAt = DateTime.UtcNow,
            Email = email,
            Attributes = new List<PlayerAttribute>()
        };
        dbContext.Players.Add(newPlayer);
        dbContext.SaveChanges();
        _playerCache[discordId] = newPlayer;
    }

    private void UpdatePlayer(string discordId, string discordName, string email)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
        
        var existingPlayer = dbContext.Players.FirstOrDefault(p => p.DiscordId == discordId);
        if (existingPlayer == null)
            throw new ArgumentException("Player can't be updated since there is no player with this id.");
            
        // Update properties on login
        existingPlayer.Name = discordName;
        existingPlayer.Email = email;
        dbContext.SaveChanges();
        _playerCache[discordId] = existingPlayer;
    }
    
    public void UpdatePlayerAttributes(Player player, Dictionary<string, string> attributes)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
        
        var existingPlayer = dbContext.Players
            .Include(p => p.Attributes)
            .FirstOrDefault(p => p.Id == player.Id);
        if (existingPlayer == null)
            throw new ArgumentException("Player can't be updated since there is no player with this id.");
            
        lock (_playerUpdateLock)
        {
            foreach (var pair in attributes)
            {
                var existingAttr = existingPlayer.Attributes.FirstOrDefault(a => a.Name == pair.Key);
                if (existingAttr != null)
                {
                    existingAttr.Value = pair.Value;
                }
                else
                {
                    existingPlayer.Attributes.Add(new PlayerAttribute()
                    {
                        Player = existingPlayer,
                        Name = pair.Key,
                        Value = pair.Value
                    });
                }
            }
            dbContext.SaveChanges();
            _playerCache[player.DiscordId] = existingPlayer;
        }
    }
}