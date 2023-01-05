using Berg.Discord;
using Berg.Options;
using Berg.Services;
using Berg.Workers;
using k8s;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("challenges.json", false, true);
builder.Services.Configure<ChallengeOptions>(builder.Configuration.GetSection("ChallengeOptions"));

var discordConfig = new DiscordAuthenticationConfig();
builder.Configuration.GetSection("DiscordAuthConfig").Bind(discordConfig);
builder.Services.AddDiscordAuthentication(discordConfig);
builder.Services.AddControllers();
builder.Services.AddRazorPages();

builder.Services.AddSingleton<ChallengeService>();
builder.Services.AddSingleton(s =>
    new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile("kubeconfig.yaml")));
builder.Services.AddHostedService<ChallengeWorker>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var challengeService = scope.ServiceProvider.GetService<ChallengeService>();
    await challengeService!.CreateSharedChallenges(CancellationToken.None);
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

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.MapControllers();
app.MapRazorPages();

app.Run();
