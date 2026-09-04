using DomainCopilot.Domain.Documents;
using DomainCopilot.Infrastructure.Persistence.Chunks;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence;

public sealed class DomainCopilotDbContext(DbContextOptions<DomainCopilotDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<ChunkRecord> Chunks => Set<ChunkRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomainCopilotDbContext).Assembly);
    }
}
