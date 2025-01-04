using System.Data.Common;
using Berg.Api.Db;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Berg.Api.Test;

public class BergDbContextFactory : IDisposable
{
    private readonly DbConnection _dbConnection;

    public static readonly Guid Team1Id = Guid.NewGuid();
    public static readonly Guid Team2Id = Guid.NewGuid();
    public static readonly Guid Team3Id = Guid.NewGuid();
    public static readonly string Challenge1Id = "challenge-1";
    public static readonly string Challenge2Id = "challenge-2";
    public static readonly string Challenge3Id = "challenge-3";
    public static readonly Guid Team1Player1Id = Guid.NewGuid();
    public static readonly Guid Team1Player2Id = Guid.NewGuid();
    public static readonly Guid Team2Player1Id = Guid.NewGuid();

    public BergDbContextFactory()
    {
        _dbConnection = new SqliteConnection("DataSource=:memory:");
        _dbConnection.Open();

        using var dbContext = CreateContext();
        dbContext.Database.EnsureCreated();

        var challenge1 = new Challenge { Name = Challenge1Id };
        var challenge2 = new Challenge { Name = Challenge2Id };
        var challenge3 = new Challenge { Name = Challenge3Id };
        dbContext.Challenges.Add(challenge1);
        dbContext.Challenges.Add(challenge2);
        dbContext.Challenges.Add(challenge3);
        dbContext.SaveChanges();

        var team1 = new Team { Id = Team1Id, Name = "Team 1", JoinToken = "join-token-1" };
        var team2 = new Team { Id = Team2Id, Name = "Team 2", JoinToken = "join-token-2" };
        var team3 = new Team { Id = Team3Id, Name = "Team 3", JoinToken = "join-token-3" };
        dbContext.Teams.Add(team1);
        dbContext.Teams.Add(team2);
        dbContext.Teams.Add(team3);
        dbContext.SaveChanges();

        var team1Player1 = new Player
        {
            Id = Team1Player1Id,
            FederatedId = "1",
            Name = "Team 1 Player 1",
            Email = "player1@team1.local",
            Team = team1,
            Attributes = new List<PlayerAttribute>
            {
                new() { Name = "Category", Value = "Junior" }
            }
        };
        var team1Player2 = new Player
        {
            Id = Team1Player2Id,
            FederatedId = "2",
            Name = "Team 1 Player 2",
            Email = "player2@team1.local",
            Team = team1,
            Attributes = new List<PlayerAttribute>
            {
                new() { Name = "Category", Value = "Senior" }
            }
        };
        var team2Player1 = new Player
        {
            Id = Team2Player1Id,
            FederatedId = "3",
            Name = "Team 2 Player 1",
            Email = "player1@team2.local",
            Team = team2,
            Attributes = new List<PlayerAttribute>
            {
                new() { Name = "Category", Value = "Junior" }
            }
        };
        dbContext.Players.Add(team1Player1);
        dbContext.Players.Add(team1Player2);
        dbContext.Players.Add(team2Player1);
        dbContext.SaveChanges();

        dbContext.Solves.Add(new Solve
        {
            Id = Guid.NewGuid(),
            Challenge = challenge1,
            Player = team1Player1,
            SolvedAt = DateTime.UtcNow
        });
        dbContext.Solves.Add(new Solve
        {
            Id = Guid.NewGuid(),
            Challenge = challenge1,
            Player = team1Player2,
            SolvedAt =  DateTime.UtcNow.AddHours(-1)
        });
        dbContext.Solves.Add(new Solve
        {
            Id = Guid.NewGuid(),
            Challenge = challenge1,
            Player = team2Player1,
            SolvedAt = DateTime.UtcNow
        });
        dbContext.Solves.Add(new Solve
        {
            Id = Guid.NewGuid(),
            Challenge = challenge2,
            Player = team2Player1,
            SolvedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }

    private DbContextOptions<BergDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<BergDbContext>()
            .UseSqlite(_dbConnection)
            .Options;
    }

    internal BergDbContext CreateContext()
    {
        return new BergDbContext(CreateOptions());
    }

    public void Dispose()
    {
        _dbConnection.Dispose();
    }
}