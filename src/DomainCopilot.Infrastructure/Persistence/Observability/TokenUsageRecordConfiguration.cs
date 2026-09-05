using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.Observability;

public sealed class TokenUsageRecordConfiguration : IEntityTypeConfiguration<TokenUsageRecord>
{
    public void Configure(EntityTypeBuilder<TokenUsageRecord> builder)
    {
        builder.ToTable("TokenUsageRecords");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.CorrelationId).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => r.CorrelationId);

        builder.Property(r => r.AgentName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ProviderName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ModelName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.EstimatedCostUsd).HasColumnType("decimal(12,6)");

        builder.HasIndex(r => r.TimestampUtc);
    }
}
