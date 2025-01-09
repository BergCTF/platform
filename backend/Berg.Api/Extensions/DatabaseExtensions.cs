using Berg.Api.Db;
using Microsoft.EntityFrameworkCore;

namespace Berg.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task InitializeAndMigrateDatabaseAsync(this WebApplication app, AsyncServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}