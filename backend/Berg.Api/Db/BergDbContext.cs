using Microsoft.EntityFrameworkCore;

namespace Berg.Api.Db;

public class BergDbContext(DbContextOptions<BergDbContext> options) : DbContext(options)
{
    public DbSet<Challenge> Challenges { get; set; } = null!;
    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<Solve> Solves { get; set; } = null!;
    public DbSet<Submission> Submissions { get; set; } = null!;
    public DbSet<Team> Teams { get; set; } = null!;
    public DbSet<Instance> Instances { get; set; } = null!;
}