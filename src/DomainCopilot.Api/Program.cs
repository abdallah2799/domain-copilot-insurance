using System.Diagnostics;
using System.Text;
using DomainCopilot.Application.Observability;
using DomainCopilot.Infrastructure;
using DomainCopilot.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Only for running `dotnet run`/the built DLL directly on a host (no Docker Compose env_file
// injection). Loads into real process env vars before configuration binding runs, so it's
// indistinguishable from a genuinely exported variable to everything downstream. No-ops (and is
// gitignored) in CI/containers, which don't have a .env file and inject real environment variables
// instead.
//
// A plain `File.Exists(".env")` looked correct but silently failed depending on how the process was
// started: `dotnet run --project src/DomainCopilot.Api` (the README's own documented command) sets
// the working directory to that project's own folder, not the repo root, so ".env" resolved to a
// path that doesn't exist there -- the app started with no connection string at all and crashed in
// DemoUserSeeder with "The ConnectionString property has not been initialized," a real failure a
// reader of the README hit. Searching upward from the assembly's own location for the solution file
// finds the repo root regardless of the current working directory the process happened to start in.
var repoRootForEnv = FindRepoRootOrNull(AppContext.BaseDirectory);
if (repoRootForEnv is not null)
{
    var envPath = Path.Combine(repoRootForEnv, ".env");
    if (File.Exists(envPath))
    {
        // clobberExistingVars: false so a real environment variable beats the file. The default
        // overwrites, which silently made `.env` the highest-precedence source: setting
        // Providers__CompletionMode or a different provider key on the command line appeared to
        // work and was then overwritten before configuration ever read it -- a run started that way
        // used the file's values while reporting nothing unusual. Treating `.env` as the fallback
        // for local development, not an override of an explicit environment variable, is both the
        // conventional semantics and the only way the documented record/replay command works.
        DotNetEnv.Env.Load(envPath, new DotNetEnv.LoadOptions(clobberExistingVars: false));
    }
}

static string? FindRepoRootOrNull(string startDirectory)
{
    var dir = startDirectory;
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir, "DomainCopilot.slnx")))
        {
            return dir;
        }

        dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
    }

    return null;
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

// FR-9 (ADR-0013): traces for every request (AspNetCore instrumentation) and every outbound HTTP
// call a provider adapter makes (HttpClient instrumentation), plus DomainCopilotActivitySource's
// own spans (an adjudication stage, an agent's completion call) -- all nested under the same
// request trace via ambient AsyncLocal Activity propagation, no manual correlation-id plumbing
// needed for that nesting. AddOtlpExporter() with no explicit endpoint reads the standard
// OTEL_EXPORTER_OTLP_ENDPOINT env var itself (the OTel .NET SDK's own contract), exported to a
// self-hosted .NET Aspire Dashboard (docker-compose's otel-collector service).
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("domain-copilot-api"))
    .WithTracing(tracing => tracing
        .AddSource(DomainCopilotActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

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

// FR-9's correlation ID: the request's own W3C trace id (ASP.NET Core starts an Activity per
// request automatically, honoring an incoming `traceparent` header if the caller sent one) --
// surfaced on the response so a caller can correlate their own logs against this one, and pushed
// into every log line this request produces via a logger scope, so "request → orchestrator →
// agent → LLM call" is greppable by one id across the whole log stream, not just visible in a
// trace viewer.
app.Use(async (context, next) =>
{
    var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        await next();
    }
});

// Skipped in Development: the documented local dev flow (Angular's dev-only CORS policy above)
// is plain HTTP end to end. Forcing a redirect here would send the browser to the "https" launch
// profile's port instead, which uses the ASP.NET Core local dev certificate -- untrusted unless
// `dotnet dev-certs https --trust` has been run on that machine, so a real request would fail with
// a genuine certificate error instead of just working, for no benefit in a same-machine dev setup.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

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
