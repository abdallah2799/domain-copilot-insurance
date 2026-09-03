using DomainCopilot.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

// Only for running `dotnet run` directly on a host (no Docker Compose env_file injection).
// Loads into real process env vars before configuration binding runs, so it's indistinguishable
// from a genuinely exported variable to everything downstream. No-ops (and is gitignored) in
// CI/containers, which don't have a .env file and inject real environment variables instead.
if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddDomainCopilotInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
// Liveness: is the process itself up — no dependency checks, so a slow/down database never makes
// an orchestrator think the process needs restarting. Readiness: can this instance actually serve
// traffic — runs the "ready"-tagged checks (MSSQL, Qdrant) registered in Infrastructure's DI.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

public partial class Program;
