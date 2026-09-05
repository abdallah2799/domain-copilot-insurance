namespace DomainCopilot.Infrastructure.Identity;

/// <summary>Bound directly from flat environment variables (JWT_SIGNING_KEY etc.), not a nested
/// configuration section — matching the naming .env.example already established for these keys
/// before FR-8 existed, and the standard OTel env var convention FR-9's ADR-0013 follows too.</summary>
public sealed class AuthOptions
{
    public string JwtSigningKey { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "domain-copilot";
    public string JwtAudience { get; set; } = "domain-copilot-clients";
    public int TokenExpiryMinutes { get; set; } = 480;

    public string SeedAdjusterUsername { get; set; } = "adjuster";
    public string SeedAdjusterPassword { get; set; } = string.Empty;
    public string SeedAnalystUsername { get; set; } = "analyst";
    public string SeedAnalystPassword { get; set; } = string.Empty;

    public static AuthOptions FromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration) => new()
    {
        JwtSigningKey = configuration["JWT_SIGNING_KEY"] ?? string.Empty,
        JwtIssuer = configuration["JWT_ISSUER"] ?? "domain-copilot",
        JwtAudience = configuration["JWT_AUDIENCE"] ?? "domain-copilot-clients",
        TokenExpiryMinutes = int.TryParse(configuration["JWT_EXPIRY_MINUTES"], out var minutes) ? minutes : 480,
        SeedAdjusterUsername = configuration["SEED_ADJUSTER_USERNAME"] ?? "adjuster",
        SeedAdjusterPassword = configuration["SEED_ADJUSTER_PASSWORD"] ?? string.Empty,
        SeedAnalystUsername = configuration["SEED_ANALYST_USERNAME"] ?? "analyst",
        SeedAnalystPassword = configuration["SEED_ANALYST_PASSWORD"] ?? string.Empty,
    };
}
