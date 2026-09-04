using DomainCopilot.Application.Adjudication;
using DomainCopilot.Application.Tests.CaseData;
using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Application.Tests.Adjudication;

public class LookupDeclarationsToolExecutorTests
{
    private static PolicyDeclaration Declaration() => PolicyDeclaration.Create(
        "MMIC-PAP-100234", "John A. Whitfield", 2021, "Honda", "Accord", "1HGCV1F34MA012345",
        "PAP-2024-STD", new DateOnly(2024, 3, 1), 100_000m, 300_000m, 50_000m, 5_000m, 100_000m, 300_000m,
        true, 500m, true, 250m, 30m, ["Roadside Assistance Endorsement (END-RA-01)"]);

    private static async Task<LookupDeclarationsToolExecutor> ExecutorWithLoadedDeclarationAsync()
    {
        var repo = new FakePolicyDeclarationRepository();
        await repo.AddAsync(Declaration());
        await repo.SaveChangesAsync();
        return new LookupDeclarationsToolExecutor(repo);
    }

    [Fact]
    public async Task Execute_KnownPolicyNumber_ReturnsDeclarationFacts()
    {
        var executor = await ExecutorWithLoadedDeclarationAsync();

        var result = await executor.ExecuteAsync("""{"policyNumber": "MMIC-PAP-100234"}""");

        Assert.True(result.Success);
        Assert.Contains("PAP-2024-STD", result.ResultJson);
        Assert.Contains("Roadside Assistance", result.ResultJson);
    }

    [Fact]
    public async Task Execute_UnknownPolicyNumber_FailsWithClearMessage()
    {
        var executor = new LookupDeclarationsToolExecutor(new FakePolicyDeclarationRepository());

        var result = await executor.ExecuteAsync("""{"policyNumber": "does-not-exist"}""");

        Assert.False(result.Success);
        Assert.Contains("does-not-exist", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_MissingPolicyNumber_Fails()
    {
        var executor = new LookupDeclarationsToolExecutor(new FakePolicyDeclarationRepository());

        var result = await executor.ExecuteAsync("{}");

        Assert.False(result.Success);
        Assert.Contains("policyNumber", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_MalformedJson_Fails()
    {
        var executor = new LookupDeclarationsToolExecutor(new FakePolicyDeclarationRepository());

        var result = await executor.ExecuteAsync("{not valid json");

        Assert.False(result.Success);
    }
}
