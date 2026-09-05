using System.Text;
using DomainCopilot.Infrastructure;
using DomainCopilot.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

// Only for running `dotnet run` directly on a host (no Docker Compose env_file injection).
// Loads into real process env vars before configuration binding runs, so it's indistinguishable
// from a genuinely exported variable to everything downstream. No-ops (and is gitignored) in
// CI/containers, which don't have a .env file and inject real environment variables instead.
if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddDomainCopilotInfrastructure(builder.Configuration);

// FR-8 (ADR-0012): every controller requires a valid bearer token by default (RequireAuthorization
// below); [AllowAnonymous] on AuthController.Login is the one deliberate exception. The signing key
// is read the same way AuthOptions itself does (a flat JWT_SIGNING_KEY env var, not a nested
// section) so both sides of token issuance/validation agree on the same configuration source.
var authOptions = AuthOptions.FromConfiguration(builder.Configuration);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = authOptions.JwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                string.IsNullOrEmpty(authOptions.JwtSigningKey) ? Guid.NewGuid().ToString() : authOptions.JwtSigningKey)),
        };
    });
builder.Services.AddAuthorization(options => options.FallbackPolicy = options.DefaultPolicy);

// Dev-only: the Angular dev server (localhost:4200) runs on a different origin than the API
// (localhost:5080).
const string AngularDevCorsPolicy = "AngularDev";
builder.Services.AddCors(options => options.AddPolicy(AngularDevCorsPolicy, policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseCors(AngularDevCorsPolicy);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Liveness: is the process itself up — no dependency checks, so a slow/down database never makes
// an orchestrator think the process needs restarting. Readiness: can this instance actually serve
// traffic — runs the "ready"-tagged checks (MSSQL, Qdrant) registered in Infrastructure's DI.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

public partial class Program;
