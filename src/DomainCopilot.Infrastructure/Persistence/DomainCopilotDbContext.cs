using DomainCopilot.Domain.Adjudication;
using DomainCopilot.Domain.CaseData;
using DomainCopilot.Domain.Documents;
using DomainCopilot.Domain.Ocr;
using DomainCopilot.Infrastructure.Persistence.Chunks;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence;

public sealed class DomainCopilotDbContext(DbContextOptions<DomainCopilotDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<ChunkRecord> Chunks => Set<ChunkRecord>();

    public DbSet<PolicyDeclaration> PolicyDeclarations => Set<PolicyDeclaration>();

    public DbSet<ClaimHistoryRecord> ClaimHistoryRecords => Set<ClaimHistoryRecord>();

    public DbSet<AdjudicationCase> AdjudicationCases => Set<AdjudicationCase>();

    public DbSet<ScannedDocument> ScannedDocuments => Set<ScannedDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomainCopilotDbContext).Assembly);
    }
}
