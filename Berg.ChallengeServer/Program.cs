using Berg.ChallengeServer.BackgroundServices;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using k8s;
using Microsoft.AspNetCore.Authentication.Cookies;
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

builder.Services.AddControllers();
builder.Services.AddSingleton(new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig()));
builder.Services.AddSingleton<WebSocketService>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<IChallengeService, ChallengeService>();
builder.Services.AddSingleton<PlayerService>();
builder.Services.AddHostedService<RefreshService>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "Discord";
    })
    .AddCookie(o =>
    {
        o.Cookie = new CookieBuilder
        {
            Name = "berg-auth",
            SecurePolicy = CookieSecurePolicy.Always,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true
        };
    })
    .AddDiscord(options =>
    {
        options.ClientId = discordConfig.ClientId;
        options.ClientSecret = discordConfig.ClientSecret;
        options.CorrelationCookie.Name = "berg-correlation.";
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CallbackPath = "/api/v1/federation-callback";
        options.Scope.Add("email");
        options.Events.OnRedirectToAuthorizationEndpoint = ctx =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/api/v1/login") &&
                !ctx.Request.Path.StartsWithSegments("/api/v1/logout"))
            {
                ctx.Response.StatusCode = 401;
            }
            else
            {
                ctx.Response.Redirect(ctx.RedirectUri);
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<BergDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BergDbConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
    dbContext.Database.Migrate();
}

app.UseWebSockets();
app.UseSwagger();
app.UseSwaggerUI();

app.Use(next => context =>
{
    // Hack to make dotnet think that the request came in over https, to make sure that redirect urls are
    // being created with a https:// prefix. Necessary because the ingress controller handles TLS termination for us.
    context.Request.Scheme = "https";
    return next(context);
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
