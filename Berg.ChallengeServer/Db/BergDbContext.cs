using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Db;

public class BergDbContext : DbContext
{
    public DbSet<Challenge> Challenges { get; set; } = null!;
    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<Solve> Solves { get; set; } = null!;
    public DbSet<Submission> Submissions { get; set; } = null!;
    public DbSet<Team> Teams { get; set; } = null!;

    public BergDbContext(DbContextOptions<BergDbContext> options) : base(options)
    {
    }
}