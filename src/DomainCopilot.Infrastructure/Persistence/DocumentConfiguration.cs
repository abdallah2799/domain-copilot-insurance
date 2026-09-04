using DomainCopilot.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.SourceId).HasMaxLength(200).IsRequired();
        builder.HasIndex(d => d.SourceId).IsUnique();

        builder.Property(d => d.Title).HasMaxLength(500).IsRequired();
        builder.Property(d => d.SourceFileName).HasMaxLength(1000).IsRequired();
        builder.Property(d => d.ContentHash).HasMaxLength(128).IsRequired();

        builder.Property(d => d.FormVersion).HasMaxLength(100);
        builder.Property(d => d.ErrorMessage).HasMaxLength(2000);

        builder.Property(d => d.Category).HasConversion<string>().HasMaxLength(50);
        builder.Property(d => d.Format).HasConversion<string>().HasMaxLength(50);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(d => d.Status);
    }
}
