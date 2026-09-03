using DomainCopilot.Domain.Documents;
using DomainCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace DomainCopilot.Integration.Tests;

/// <summary>
/// Runs against a real, ephemeral MSSQL container (Testcontainers) — not a mock — so it actually
/// exercises the EF Core mapping (DocumentConfiguration) and SQL Server's own unique-index
/// enforcement, not just in-memory LINQ semantics.
/// </summary>
public sealed class DocumentRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private DomainCopilotDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<DomainCopilotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        _dbContext = new DomainCopilotDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    private static Document NewDocument(string sourceId = "policy_wording_v1", string contentHash = "hash-1") =>
        Document.Create(
            sourceId: sourceId,
            title: "Policy Wording — PAP-2024-STD",
            category: DocumentCategory.PolicyForm,
            format: DocumentFormat.Pdf,
            sourceFileName: "policy-forms/policy_wording_v1.pdf",
            contentHash: contentHash,
            formVersion: "PAP-2024-STD");

    [Fact]
    public async Task AddAndSave_ThenFindBySourceId_RoundTripsAllFields()
    {
        var repo = new DocumentRepository(_dbContext);
        var doc = NewDocument();

        await repo.AddAsync(doc);
        await repo.SaveChangesAsync();

        var reloaded = await repo.FindBySourceIdAsync("policy_wording_v1");

        Assert.NotNull(reloaded);
        Assert.Equal(doc.Id, reloaded!.Id);
        Assert.Equal("PAP-2024-STD", reloaded.FormVersion);
        Assert.Equal(IngestionStatus.Pending, reloaded.Status);
    }

    [Fact]
    public async Task FindBySourceId_WhenNotIngestedYet_ReturnsNull()
    {
        var repo = new DocumentRepository(_dbContext);

        var result = await repo.FindBySourceIdAsync("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task SourceId_IsEnforcedUniqueBySqlServer_NotJustByConvention()
    {
        var repo = new DocumentRepository(_dbContext);
        await repo.AddAsync(NewDocument(sourceId: "dup", contentHash: "hash-a"));
        await repo.SaveChangesAsync();

        // A fresh DbContext against the same database — proves the constraint is enforced by SQL
        // Server itself, not just by this DbContext instance's change tracker noticing a duplicate.
        var options = new DbContextOptionsBuilder<DomainCopilotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        await using var otherContext = new DomainCopilotDbContext(options);
        var otherRepo = new DocumentRepository(otherContext);
        await otherRepo.AddAsync(NewDocument(sourceId: "dup", contentHash: "hash-b"));

        await Assert.ThrowsAsync<DbUpdateException>(() => otherRepo.SaveChangesAsync());
    }

    [Fact]
    public async Task ListByStatus_AfterMarkFailed_ReturnsTheFailedDocument()
    {
        var repo = new DocumentRepository(_dbContext);
        var doc = NewDocument(sourceId: "will-fail", contentHash: "hash-c");
        doc.BeginProcessing();
        doc.MarkFailed("OCR engine unavailable");
        await repo.AddAsync(doc);
        await repo.SaveChangesAsync();

        var failed = await repo.ListByStatusAsync(IngestionStatus.Failed);

        Assert.Contains(failed, d => d.SourceId == "will-fail" && d.ErrorMessage == "OCR engine unavailable");
    }
}
