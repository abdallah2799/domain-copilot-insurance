using DomainCopilot.Application.Adjudication;
using DomainCopilot.Domain.Adjudication;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.Adjudication;

public sealed class AdjudicationCaseRepository(DomainCopilotDbContext dbContext) : IAdjudicationCaseRepository
{
    public Task<AdjudicationCase?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AdjudicationCases.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AdjudicationCase>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.AdjudicationCases.OrderByDescending(a => a.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task AddAsync(AdjudicationCase adjudicationCase, CancellationToken cancellationToken = default)
    {
        dbContext.AdjudicationCases.Add(adjudicationCase);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
