using DomainCopilot.Domain.Ocr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Ocr;

public sealed class ScannedDocumentConfiguration : IEntityTypeConfiguration<ScannedDocument>
{
    public void Configure(EntityTypeBuilder<ScannedDocument> builder)
    {
        builder.ToTable("ScannedDocuments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.ClaimNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(d => d.ClaimNumber);

        builder.Property(d => d.SourceFileName).HasMaxLength(260).IsRequired();
        builder.Property(d => d.ContentHash).HasMaxLength(128).IsRequired();
        // Idempotency lookup key (OcrIngestionService.FindByContentHashAsync) -- not globally
        // unique on ContentHash alone, since the same exact bytes reused across two different
        // claims (unlikely but not invalid) must not collide.
        builder.HasIndex(d => new { d.ClaimNumber, d.ContentHash });

        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(d => d.ErrorMessage).HasMaxLength(2000);
        builder.Property(d => d.PageResultsJson);
        builder.Property(d => d.CombinedText);
    }
}
