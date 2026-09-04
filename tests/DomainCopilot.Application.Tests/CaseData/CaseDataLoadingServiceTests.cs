using DomainCopilot.Application.CaseData;

namespace DomainCopilot.Application.Tests.CaseData;

public class CaseDataLoadingServiceTests
{
    private static LoadPolicyDeclarationRequest Declaration(string policyNumber = "MMIC-PAP-100234") => new(
        policyNumber, "John A. Whitfield", 2021, "Honda", "Accord", "1HGCV1F34MA012345",
        "PAP-2024-STD", new DateOnly(2024, 3, 1), 100_000m, 300_000m, 50_000m, 5_000m, 100_000m, 300_000m,
        true, 500m, true, 250m, 30m, ["Roadside Assistance Endorsement (END-RA-01)"]);

    private static LoadClaimHistoryRequest Claim(string claimNumber = "CLM-2025-04417") => new(
        claimNumber, "MMIC-PAP-100234", new DateOnly(2025, 8, 3), "Collision",
        "Insured vehicle struck another vehicle's rear bumper.", 3_200m, "DPD-2025-118834", false, null);

    [Fact]
    public async Task LoadAsync_NewDeclarationAndClaim_LoadsBoth()
    {
        var service = new CaseDataLoadingService(new FakePolicyDeclarationRepository(), new FakeClaimHistoryRepository());

        var result = await service.LoadAsync([Declaration()], [Claim()]);

        Assert.Equal(1, result.DeclarationsLoaded);
        Assert.Equal(0, result.DeclarationsSkipped);
        Assert.Equal(1, result.ClaimsLoaded);
        Assert.Equal(0, result.ClaimsSkipped);
    }

    [Fact]
    public async Task LoadAsync_AlreadyLoadedPolicyNumber_IsSkippedNotDuplicated()
    {
        var declarationRepo = new FakePolicyDeclarationRepository();
        var service = new CaseDataLoadingService(declarationRepo, new FakeClaimHistoryRepository());
        await service.LoadAsync([Declaration()], []);

        var result = await service.LoadAsync([Declaration()], []);

        Assert.Equal(0, result.DeclarationsLoaded);
        Assert.Equal(1, result.DeclarationsSkipped);
        Assert.Single(await declarationRepo.ListAllAsync());
    }

    [Fact]
    public async Task LoadAsync_AlreadyLoadedClaimNumber_IsSkippedNotDuplicated()
    {
        var claimRepo = new FakeClaimHistoryRepository();
        var service = new CaseDataLoadingService(new FakePolicyDeclarationRepository(), claimRepo);
        await service.LoadAsync([], [Claim()]);

        var result = await service.LoadAsync([], [Claim()]);

        Assert.Equal(0, result.ClaimsLoaded);
        Assert.Equal(1, result.ClaimsSkipped);
        Assert.Single(await claimRepo.ListAllAsync());
    }

    [Fact]
    public async Task LoadAsync_UnrecognizedLossType_ThrowsRatherThanSilentlyMisclassifying()
    {
        var service = new CaseDataLoadingService(new FakePolicyDeclarationRepository(), new FakeClaimHistoryRepository());
        var badClaim = Claim() with { LossType = "Theft" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadAsync([], [badClaim]));
    }

    [Fact]
    public async Task LoadAsync_UmUimLossType_IsRecognized()
    {
        // "UM/UIM" isn't a valid C# enum literal, so this is specifically checking the explicit
        // mapping in ParseLossType, not a bare Enum.TryParse — a real corpus value that a first-pass
        // 3-way enum (Collision/Comprehensive/Liability) missed entirely.
        var claimRepo = new FakeClaimHistoryRepository();
        var service = new CaseDataLoadingService(new FakePolicyDeclarationRepository(), claimRepo);
        var umUimClaim = Claim() with { LossType = "UM/UIM" };

        var result = await service.LoadAsync([], [umUimClaim]);

        Assert.Equal(1, result.ClaimsLoaded);
    }

    [Fact]
    public async Task LoadAsync_MultipleNewRecords_LoadsAllOfThem()
    {
        var service = new CaseDataLoadingService(new FakePolicyDeclarationRepository(), new FakeClaimHistoryRepository());

        var result = await service.LoadAsync(
            [Declaration("MMIC-PAP-1"), Declaration("MMIC-PAP-2")],
            [Claim("CLM-1"), Claim("CLM-2"), Claim("CLM-3")]);

        Assert.Equal(2, result.DeclarationsLoaded);
        Assert.Equal(3, result.ClaimsLoaded);
    }
}
