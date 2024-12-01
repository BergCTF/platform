using Berg.Api.Db;
using Microsoft.EntityFrameworkCore;

namespace Berg.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task InitializeAndMigrateDatabaseAsync(this WebApplication app, AsyncServiceScope scope)
    {
        var bergDbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
        await bergDbContext.Database.MigrateAsync();
    }
}