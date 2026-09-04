using DomainCopilot.Domain.CaseData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.CaseData;

public sealed class ClaimHistoryRecordConfiguration : IEntityTypeConfiguration<ClaimHistoryRecord>
{
    public void Configure(EntityTypeBuilder<ClaimHistoryRecord> builder)
    {
        builder.ToTable("ClaimHistoryRecords");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ClaimNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.ClaimNumber).IsUnique();

        builder.Property(c => c.PolicyNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.PolicyNumber);

        builder.Property(c => c.PoliceReportNumber).HasMaxLength(50);
        builder.Property(c => c.FlaggedAnomaly).HasMaxLength(500);
        builder.Property(c => c.LossType).HasConversion<string>().HasMaxLength(50);

        // Explicit precision — see PolicyDeclarationConfiguration for why this isn't left to
        // EF Core's silently-truncating default.
        builder.Property(c => c.EstimatedDamage).HasPrecision(18, 2);
    }
}
