using Microsoft.EntityFrameworkCore;

namespace Berg.Db;

public class BergDbContext : DbContext
{
    public DbSet<Solve> Solves { get; set; } = null!;
    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<Challenge> Challenges { get; set; } = null!;
    public DbSet<Submission> Submissions { get; set; } = null!;

    public BergDbContext(DbContextOptions options) : base(options)
    {
    }
}