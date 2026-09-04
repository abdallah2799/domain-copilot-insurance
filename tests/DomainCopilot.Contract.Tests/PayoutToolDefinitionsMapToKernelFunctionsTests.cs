using DomainCopilot.Application.Adjudication;
using DomainCopilot.Infrastructure.Providers;

namespace DomainCopilot.Contract.Tests;

/// <summary>
/// Confirms the four deterministic payout tools' JSON Schemas actually map onto Semantic Kernel
/// function metadata without error — a malformed schema here would only surface once an
/// orchestrator tried to register these tools with a real model, much later and harder to trace
/// back to its source.
/// </summary>
public class PayoutToolDefinitionsMapToKernelFunctionsTests
{
    public static IEnumerable<object[]> Executors()
    {
        yield return [new StandardPayoutToolExecutor()];
        yield return [new TotalLossDeterminationToolExecutor()];
        yield return [new TotalLossSettlementToolExecutor()];
        yield return [new GapCoverageToolExecutor()];
    }

    [Theory]
    [MemberData(nameof(Executors))]
    public void ToKernelFunction_MapsWithoutError_AndPreservesRequiredParameters(IPayoutToolExecutor executor)
    {
        var function = KernelToolMapper.ToKernelFunction(executor.Definition);

        Assert.Equal(executor.Definition.Name, function.Name);
        Assert.NotEmpty(function.Metadata.Parameters);
        Assert.Contains(function.Metadata.Parameters, p => p.IsRequired);
    }
}
