using DomainCopilot.Application.CaseData;
using DomainCopilot.Domain.CaseData;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.CaseData;

public sealed class ClaimHistoryRepository(DomainCopilotDbContext dbContext) : IClaimHistoryRepository
{
    public Task<ClaimHistoryRecord?> FindByClaimNumberAsync(string claimNumber, CancellationToken cancellationToken = default) =>
        dbContext.ClaimHistoryRecords.SingleOrDefaultAsync(c => c.ClaimNumber == claimNumber, cancellationToken);

    public async Task<IReadOnlyList<ClaimHistoryRecord>> FindByPolicyNumberWithinWindowAsync(
        string policyNumber, DateOnly referenceDate, int windowDays, CancellationToken cancellationToken = default)
    {
        var earliest = referenceDate.AddDays(-windowDays);
        var latest = referenceDate.AddDays(windowDays);

        return await dbContext.ClaimHistoryRecords
            .Where(c => c.PolicyNumber == policyNumber && c.DateOfLoss >= earliest && c.DateOfLoss <= latest)
            .OrderBy(c => c.DateOfLoss)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClaimHistoryRecord>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ClaimHistoryRecords.OrderBy(c => c.ClaimNumber).ToListAsync(cancellationToken);

    public Task AddAsync(ClaimHistoryRecord record, CancellationToken cancellationToken = default)
    {
        dbContext.ClaimHistoryRecords.Add(record);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
