using System.Text.Json;
using DomainCopilot.Domain.CaseData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DomainCopilot.Infrastructure.Persistence.CaseData;

public sealed class PolicyDeclarationConfiguration : IEntityTypeConfiguration<PolicyDeclaration>
{
    public void Configure(EntityTypeBuilder<PolicyDeclaration> builder)
    {
        builder.ToTable("PolicyDeclarations");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PolicyNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => p.PolicyNumber).IsUnique();

        builder.Property(p => p.NamedInsured).HasMaxLength(200).IsRequired();
        builder.Property(p => p.VehicleMake).HasMaxLength(100);
        builder.Property(p => p.VehicleModel).HasMaxLength(100);
        builder.Property(p => p.Vin).HasMaxLength(50);
        builder.Property(p => p.FormVersion).HasMaxLength(100).IsRequired();

        // Explicit precision on every money column — EF Core's default (18,2) would otherwise be
        // used silently, and this codebase treats a silently-truncated figure as a real risk.
        builder.Property(p => p.LiabilityBiPerPerson).HasPrecision(18, 2);
        builder.Property(p => p.LiabilityBiPerAccident).HasPrecision(18, 2);
        builder.Property(p => p.LiabilityPd).HasPrecision(18, 2);
        builder.Property(p => p.MedPay).HasPrecision(18, 2);
        builder.Property(p => p.UmUimPerPerson).HasPrecision(18, 2);
        builder.Property(p => p.UmUimPerAccident).HasPrecision(18, 2);
        builder.Property(p => p.CollisionDeductible).HasPrecision(18, 2);
        builder.Property(p => p.ComprehensiveDeductible).HasPrecision(18, 2);
        builder.Property(p => p.RentalReimbursementDaily).HasPrecision(18, 2);

        // A list of endorsement names has no natural relational shape here and is never queried by
        // element — stored as a JSON string, same tradeoff as any other "small, whole-value" list.
        var endorsementsComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        builder.Property(p => p.Endorsements)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(endorsementsComparer);
    }
}
