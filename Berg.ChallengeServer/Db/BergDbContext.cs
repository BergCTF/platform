using Microsoft.EntityFrameworkCore;

namespace Berg.ChallengeServer.Db;

public class BergDbContext : DbContext
{
    private bool _isTesting = false;
    
    public DbSet<Challenge> Challenges { get; set; } = null!;
    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<Solve> Solves { get; set; } = null!;
    public DbSet<Submission> Submissions { get; set; } = null!;
    public DbSet<Team> Teams { get; set; } = null!;

    public BergDbContext(DbContextOptions<BergDbContext> options) : base(options)
    {
    }
    
    public BergDbContext(DbContextOptions<BergDbContext> options, bool isTesting) : base(options)
    {
        _isTesting = isTesting;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (_isTesting)
        {
            // Hack for SQLite since it doesn't support properties of type Dictionary<string, string>
            modelBuilder.Entity<Player>().Ignore(nameof(Player.Attributes));
        }
        base.OnModelCreating(modelBuilder);
    }
}