using DomainCopilot.Domain.Adjudication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Adjudication;

public sealed class AdjudicationCaseConfiguration : IEntityTypeConfiguration<AdjudicationCase>
{
    public void Configure(EntityTypeBuilder<AdjudicationCase> builder)
    {
        builder.ToTable("AdjudicationCases");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ClaimNumber).HasMaxLength(50).IsRequired();
        // Not unique — a claim can be reopened (Claim Reopening and Internal Appeals Guide), which
        // is a new adjudication run against the same claim number, not an update to the old one.
        builder.HasIndex(a => a.ClaimNumber);

        builder.Property(a => a.PolicyNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(a => a.PolicyNumber);

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(a => a.Status);

        builder.Property(a => a.ApprovedBy).HasMaxLength(200);
        builder.Property(a => a.AdjusterComments).HasMaxLength(2000);
        builder.Property(a => a.FailureReason).HasMaxLength(2000);

        builder.Property(a => a.CoverageMatchResultJson);
        builder.Property(a => a.AnomalyFindingsJson);
        builder.Property(a => a.ExclusionAnalysisResultJson);
        builder.Property(a => a.RecommendationJson);
    }
}
