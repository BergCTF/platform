using Berg.ChallengeServer.BackgroundServices;
using Berg.ChallengeServer.Configuration;
using Berg.ChallengeServer.Db;
using Berg.ChallengeServer.Services;
using k8s;
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
builder.Services.AddHostedService<RefreshService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BergDbContext>();
    // TODO: Switch to migrations
    dbContext.Database.EnsureCreated();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();