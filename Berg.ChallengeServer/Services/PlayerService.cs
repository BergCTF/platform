using System.Security.Claims;
using Berg.ChallengeServer.Db;

namespace Berg.ChallengeServer.Services;

public class PlayerService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    
    public PlayerService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Player GetPlayer(ClaimsPrincipal user)
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
                return player;
            
            // Handle discord username changes
            player.Name = discordName;
            dbContext.SaveChanges();
            return player;
        }

        var newPlayer = new Player
        {
            Id = Guid.NewGuid(),
            DiscordId = discordId,
            Name = discordName
        };
        dbContext.Players.Add(newPlayer);
        dbContext.SaveChanges();
        return newPlayer;
    }
}