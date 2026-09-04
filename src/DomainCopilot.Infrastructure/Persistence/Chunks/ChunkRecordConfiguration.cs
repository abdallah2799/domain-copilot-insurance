using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Chunks;

public sealed class ChunkRecordConfiguration : IEntityTypeConfiguration<ChunkRecord>
{
    public void Configure(EntityTypeBuilder<ChunkRecord> builder)
    {
        builder.ToTable("Chunks");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.SectionTitle).HasMaxLength(500).IsRequired();
        builder.Property(c => c.FormVersion).HasMaxLength(100);
        // No max length — chunk text (up to 400 words plus overlap) can exceed a fixed nvarchar
        // bound; EF Core maps an unbounded string property to nvarchar(max) on SQL Server.
        builder.Property(c => c.Text).IsRequired();
        builder.Property(c => c.Category).HasConversion<string>().HasMaxLength(50);

        // One row per (document, chunk index) — re-indexing deletes-and-reinserts a document's rows
        // rather than upserting in place, so this is a plain lookup index, not a uniqueness guard.
        builder.HasIndex(c => c.DocumentId);
    }
}
