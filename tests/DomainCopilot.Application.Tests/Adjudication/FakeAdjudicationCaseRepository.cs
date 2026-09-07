using DomainCopilot.Application.Adjudication;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

internal sealed class FakeAdjudicationCaseRepository : IAdjudicationCaseRepository
{
    private readonly Dictionary<Guid, AdjudicationCase> _byId = new();

    public Task<AdjudicationCase?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    // No change tracker here, so the distinction the real repository draws does not exist -- the
    // dictionary is always the current state. Kept as a separate member so the port stays honest.
    public Task<AdjudicationCase?> FindByIdForReadAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    public Task<IReadOnlyList<AdjudicationCase>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AdjudicationCase>>([.. _byId.Values]);

    public Task<IReadOnlyList<AdjudicationCase>> ListByCreatedByAsync(string createdByUsername, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AdjudicationCase>>([.. _byId.Values.Where(a => a.CreatedByUsername == createdByUsername)]);

    public Task AddAsync(AdjudicationCase adjudicationCase, CancellationToken cancellationToken = default)
    {
        _byId[adjudicationCase.Id] = adjudicationCase;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
