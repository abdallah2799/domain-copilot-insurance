using DomainCopilot.Application.CaseData;
using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Application.Tests.CaseData;

internal sealed class FakeClaimHistoryRepository : IClaimHistoryRepository
{
    private readonly Dictionary<string, ClaimHistoryRecord> _byClaimNumber = new();
    private readonly List<ClaimHistoryRecord> _pending = [];

    public Task<ClaimHistoryRecord?> FindByClaimNumberAsync(string claimNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byClaimNumber.GetValueOrDefault(claimNumber));

    public Task<IReadOnlyList<ClaimHistoryRecord>> FindByPolicyNumberWithinWindowAsync(
        string policyNumber, DateOnly referenceDate, int windowDays, CancellationToken cancellationToken = default)
    {
        var earliest = referenceDate.AddDays(-windowDays);
        var latest = referenceDate.AddDays(windowDays);

        IReadOnlyList<ClaimHistoryRecord> results = [.. _byClaimNumber.Values
            .Where(c => c.PolicyNumber == policyNumber && c.DateOfLoss >= earliest && c.DateOfLoss <= latest)];

        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<ClaimHistoryRecord>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ClaimHistoryRecord>>([.. _byClaimNumber.Values]);

    public Task AddAsync(ClaimHistoryRecord record, CancellationToken cancellationToken = default)
    {
        _pending.Add(record);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var record in _pending)
        {
            _byClaimNumber[record.ClaimNumber] = record;
        }

        _pending.Clear();
        return Task.CompletedTask;
    }
}
