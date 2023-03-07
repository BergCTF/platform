using Berg.Db;
using Berg.DiscordIntegration;
using Microsoft.EntityFrameworkCore;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<DiscordConfiguration>(ctx.Configuration.GetSection("DiscordConfig"));
        services.AddDbContext<BergDbContext>(options =>
            options.UseNpgsql(ctx.Configuration.GetConnectionString("BergContext")));
        services.AddHostedService<NotificationWorker>();
    })
    .Build();

host.Run();