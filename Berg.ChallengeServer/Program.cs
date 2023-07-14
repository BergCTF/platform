using Berg.ChallengeServer.BackgroundServices;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using k8s;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Services.AddControllers();
var ctfConfig = new CtfConfig();
builder.Configuration.GetSection("ctf").Bind(ctfConfig);
builder.Services.AddSingleton(ctfConfig);
builder.Services.AddSingleton(new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig()));
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<ChallengeService>();
builder.Services.AddSingleton<PlayerService>();
builder.Services.AddHostedService<RefreshService>();
var discordConfig = new DiscordConfig();
builder.Configuration.GetSection("discordConfig").Bind(discordConfig);
builder.Services.AddSingleton(discordConfig);

var joinUrl = $"https://discord.com/api/oauth2/authorize?client_id={discordConfig.ClientId}&permissions=2048&scope=bot";
Console.WriteLine($"Add the discord bot to your server: {joinUrl}");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "Discord";
    })
    .AddDiscord(options =>
    {
        options.ClientId = discordConfig.ClientId;
        options.ClientSecret = discordConfig.ClientSecret;
        options.CorrelationCookie.Name = "berg-correlation.";
        options.CallbackPath = "/api/v1/callback-discord";
    })
    .AddCookie(o =>
    {
        o.Events = new()
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = 401;
                }
                else
                {
                    ctx.Response.Redirect(ctx.RedirectUri);
                }
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = 403;
                }
                else
                {
                    ctx.Response.Redirect(ctx.RedirectUri);
                }
                return Task.CompletedTask;
            }
        };
        o.Cookie = new CookieBuilder
        {
            Name = "berg-auth",
            SecurePolicy = CookieSecurePolicy.Always,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (builder.Environment.IsDevelopment())
{
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open();
    builder.Services.AddDbContext<BergDbContext>(options =>
        options.UseSqlite(connection));
}
else
{
    builder.Services.AddDbContext<BergDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("BergDbConnection")));
}

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
    // TODO: Switch to migrations
    dbContext.Database.EnsureCreated();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();