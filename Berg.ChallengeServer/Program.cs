using Berg.ChallengeServer.Configuration;
using k8s;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Services.AddControllers();
var ctfConfig = new CtfConfig();
builder.Configuration.GetSection("ctf").Bind(ctfConfig);
builder.Services.AddSingleton(ctfConfig);
builder.Services.AddSingleton(s =>
    new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig()));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();