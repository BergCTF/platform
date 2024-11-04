using Berg.Api.BackgroundServices;
using Berg.Api.Configuration;
using Berg.Api.Db;
using Berg.Api.Extensions;
using Berg.Api.Services;
using k8s;
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
builder.Services.AddSingleton<WebSocketService>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<IChallengeService, ChallengeService>();
builder.Services.AddHostedService<RefreshService>();
builder.Services.AddDbContext<BergDbContext>(options => {
    options.UseNpgsql(builder.Configuration.GetConnectionString("BergDbConnection"));
    options.UseOpenIddict();
});

builder.AddSwagger(infraConfig);
builder.AddOpenIddict(kubernetes, discordConfig, genericOpenIdConfig);
builder.AddOpenTelemetryExporters(infraConfig);
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

app.UseWebSockets();
app.UseSwagger();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
