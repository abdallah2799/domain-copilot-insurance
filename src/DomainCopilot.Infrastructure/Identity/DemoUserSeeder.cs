using DomainCopilot.Application.Identity;
using DomainCopilot.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Infrastructure.Identity;

/// <summary>Seeds the two demo accounts FR-8 needs to actually demo (one per role) the first time
/// the app starts against an empty Users table. This is a deliberate, documented cut from real user
/// management (ADR-0012): there is no self-service registration, since letting anyone register as
/// Adjuster would defeat the whole point of a server-enforced role. A real deployment would replace
/// this with an admin-driven user-provisioning flow instead.</summary>
public sealed class DemoUserSeeder(IServiceScopeFactory scopeFactory, AuthOptions options, ILogger<DemoUserSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (await userRepository.AnyAsync(cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.SeedAdjusterPassword) || string.IsNullOrWhiteSpace(options.SeedAnalystPassword))
        {
            logger.LogWarning(
                "SEED_ADJUSTER_PASSWORD/SEED_ANALYST_PASSWORD are not configured -- no demo users were created. Set them in .env (see .env.example) to seed {Adjuster}/{Analyst} accounts.",
                options.SeedAdjusterUsername, options.SeedAnalystUsername);
            return;
        }

        await userRepository.AddAsync(User.Create(options.SeedAdjusterUsername, passwordHasher.Hash(options.SeedAdjusterPassword), UserRole.Adjuster), cancellationToken);
        await userRepository.AddAsync(User.Create(options.SeedAnalystUsername, passwordHasher.Hash(options.SeedAnalystPassword), UserRole.Analyst), cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded demo users {Adjuster} (Adjuster) and {Analyst} (Analyst).", options.SeedAdjusterUsername, options.SeedAnalystUsername);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
