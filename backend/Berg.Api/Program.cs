using Berg.Api.BackgroundServices;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Extensions;
using Berg.Api.Services;
using k8s;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

var ctfConfig = new CtfConfig();
builder.Configuration.GetSection("Ctf").Bind(ctfConfig);
builder.Services.AddSingleton(ctfConfig);

var infraConfig = new InfraConfig();
builder.Configuration.GetSection("Infra").Bind(infraConfig);
builder.Services.AddSingleton(infraConfig);

var discordConfig = new DiscordConfig();
builder.Configuration.GetSection("Discord").Bind(discordConfig);
builder.Services.AddSingleton(discordConfig);

var genericOpenIdConfig = new GenericOpenIdConfig();
builder.Configuration.GetSection("GenericOpenId").Bind(genericOpenIdConfig);
builder.Services.AddSingleton(genericOpenIdConfig);

var kubernetes = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
builder.Services.AddSingleton(kubernetes);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddWebSockets(options => {
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<IChallengeService, ChallengeService>();
builder.Services.AddSingleton<IWebSocketService, WebSocketService>();
builder.Services.AddSingleton<IDynamicFlagExecutableService, DynamicFlagExecutableService>();
builder.Services.AddHostedService<WatchService>();
builder.Services.AddHostedService<RefreshService>();
builder.Services.AddDbContext<BergDbContext>(options => {
    var connString = builder.Configuration.GetConnectionString("BergDbConnection");
    if (string.IsNullOrEmpty(connString))
    {
        var host = Environment.GetEnvironmentVariable("PGHOST");
        var port = Environment.GetEnvironmentVariable("PGPORT");
        var database = Environment.GetEnvironmentVariable("PGDATABASE");
        var username = Environment.GetEnvironmentVariable("PGUSER");
        var password = Environment.GetEnvironmentVariable("PGPASSWORD");
        connString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }
    options.UseNpgsql(connString);
    options.UseOpenIddict();
});

builder.Services.AddSingleton<BergMetrics>();
builder.AddOpenTelemetryExporters(infraConfig);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(infraConfig.CorsOrigins?.ToArray() ?? []);
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
    });
});

builder.AddSwagger(infraConfig);
builder.AddOpenIddict(kubernetes, infraConfig, discordConfig, genericOpenIdConfig);
builder.AddMediatR();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await app.InitializeAndMigrateDatabaseAsync(scope);
    await app.InitializeOpenIddictApplicationsAsync(scope);
}

app.MapHealthChecks("/healthz");

app.UseForwardedHeaders();
app.UseHsts();

app.UseCors();

app.UseWebSockets();
app.UseSwagger();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
