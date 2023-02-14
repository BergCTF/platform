using Berg.Db;
using Berg.Discord;
using Berg.Configuration;
using Berg.Middleware;
using Berg.Services;
using Berg.Workers;
using k8s;
using k8s.Autorest;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

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
builder.Services.AddAntiforgery(options =>
{
    options.Cookie = new CookieBuilder
    {
        Name = "csrf-cookie"
    };
    options.FormFieldName = "csrf-token";
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
});
builder.Services.AddSession(o => o.Cookie = new CookieBuilder
{
    Name = "session",
    HttpOnly = true,
    SameSite = SameSiteMode.Lax
});
builder.Services.AddControllersWithViews();
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
    try
    {
        await challengeService.CreateSharedChallenges(CancellationToken.None);
    }
    catch (HttpOperationException ex)
    {
        Console.Error.WriteLine("Exception during creation of shared challenges");
        Console.Error.WriteLine(ex.Response.Content);
        return;
    }

    var dbContext = scope.ServiceProvider.GetService<BergDbContext>()!;
    dbContext.Database.Migrate();
    
    ChallengeLoader.LoadFromConfig(dbContext, ctfInfo);
    
    if (app.Environment.IsDevelopment())
    {
        dbContext.Players.RemoveRange(dbContext.Players);
        
        var random = new Random();
        var categoryValues = Enum.GetValues<Category>();
        var challenges = dbContext.Challenges.ToList();
        for (var i = 0; i < 9; i++)
        {
            var player = new Player
            {
                Name = $"Demo#000{i}",
                Category = categoryValues[random.Next(categoryValues.Length)],
                Email = $"demo.000{i}@example.com",
                DiscordId = "299478604809764876",
                DiscordAvatarId = "3168495edc04b8b00c66dcd3c54c5763",
                CreatedAt = DateTime.UtcNow,
                Solves = new List<Solve>()
            };
                
            foreach (var challenge in challenges.Where(_ => random.Next(2) == 1))
            {
                player.Solves.Add(new Solve
                {
                    Challenge = challenge,
                    SolvedAt = DateTime.UtcNow.AddSeconds(-random.Next(100000))
                });
            }
            dbContext.Players.Add(player);
        }
        dbContext.SaveChanges();
    }
    
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
    app.UseHsts();
}

app.UseResponseCaching();
app.UseResponseCompression();
app.UseStaticFiles();

app.UseCSP();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UsePlayerRegistration();

app.MapControllers();
app.MapRazorPages();

app.Run();
