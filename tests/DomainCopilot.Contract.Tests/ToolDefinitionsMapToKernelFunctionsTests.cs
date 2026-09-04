using DomainCopilot.Application.Adjudication;
using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Providers;

namespace DomainCopilot.Contract.Tests;

/// <summary>
/// Confirms every agent tool's JSON Schema actually maps onto Semantic Kernel function metadata
/// without error — a malformed schema here would only surface once an orchestrator tried to
/// register these tools with a real model, much later and harder to trace back to its source. The
/// lookup executors are constructed with a null repository — safe here since only
/// <c>Definition</c> is exercised, never <c>ExecuteAsync</c>.
/// </summary>
public class ToolDefinitionsMapToKernelFunctionsTests
{
    public static IEnumerable<object[]> Executors()
    {
        yield return [new StandardPayoutToolExecutor()];
        yield return [new TotalLossDeterminationToolExecutor()];
        yield return [new TotalLossSettlementToolExecutor()];
        yield return [new GapCoverageToolExecutor()];
        yield return [new LookupDeclarationsToolExecutor(null!)];
        yield return [new LookupClaimHistoryToolExecutor(null!)];
        yield return [new FinalizeAdjudicationDecisionToolExecutor(null!)];
    }

    [Theory]
    [MemberData(nameof(Executors))]
    public void ToKernelFunction_MapsWithoutError_AndPreservesRequiredParameters(IToolExecutor executor)
    {
        var function = KernelToolMapper.ToKernelFunction(executor.Definition);

        Assert.Equal(executor.Definition.Name, function.Name);
        Assert.NotEmpty(function.Metadata.Parameters);
        Assert.Contains(function.Metadata.Parameters, p => p.IsRequired);
    }
}
