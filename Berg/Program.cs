using Berg.Db;
using Berg.Discord;
using Berg.Configuration;
using Berg.Middleware;
using Berg.Services;
using Berg.Workers;
using k8s;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ctf.json", false, true);
var ctfInfo = new CtfInfo();
builder.Configuration.GetSection("CtfInfo").Bind(ctfInfo);
builder.Services.AddSingleton(ctfInfo);
builder.Services.AddResponseCaching();
builder.Services.AddResponseCompression();
builder.Services.AddDistributedMemoryCache();

var discordConfig = new DiscordAuthenticationInfo();
builder.Configuration.GetSection("DiscordAuthConfig").Bind(discordConfig);
builder.Services.AddDiscordAuthentication(discordConfig);
builder.Services.AddSession();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

builder.Services.AddSingleton<ChallengeService>();
builder.Services.AddSingleton<ScoreService>();
builder.Services.AddSingleton(s =>
    new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile("kubeconfig.yaml")));
builder.Services.AddHostedService<ChallengeWorker>();
builder.Services.AddDbContext<BergDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BergContext")));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var challengeService = scope.ServiceProvider.GetService<ChallengeService>()!;
    await challengeService.CreateSharedChallenges(CancellationToken.None);

    var dbContext = scope.ServiceProvider.GetService<BergDbContext>()!;
    dbContext.Database.Migrate();
    
    ChallengeLoader.LoadFromConfig(dbContext, ctfInfo);
    
    var scoreService = scope.ServiceProvider.GetService<ScoreService>()!;
    scoreService.RecalculateScores(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHttpsRedirection();
}

app.UseResponseCaching();
app.UseResponseCompression();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UsePlayerRegistration();

app.MapControllers();
app.MapRazorPages();

app.Run();
