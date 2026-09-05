using DomainCopilot.Application.Identity;
using DomainCopilot.Domain.Identity;

namespace DomainCopilot.Application.Tests.Identity;

public class AuthServiceTests
{
    private static AuthService CreateSut(FakeUserRepository repository, FakePasswordHasher hasher) =>
        new(repository, hasher, new FakeTokenService());

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTokenAndRole()
    {
        var repository = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        await repository.AddAsync(User.Create("adjuster", hasher.Hash("correct-password"), UserRole.Adjuster));
        var sut = CreateSut(repository, hasher);

        var result = await sut.LoginAsync("adjuster", "correct-password");

        Assert.NotNull(result);
        Assert.Equal("token-for:adjuster:Adjuster", result!.Token);
        Assert.Equal("adjuster", result.Username);
        Assert.Equal("Adjuster", result.Role);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        var repository = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        await repository.AddAsync(User.Create("analyst", hasher.Hash("correct-password"), UserRole.Analyst));
        var sut = CreateSut(repository, hasher);

        var result = await sut.LoginAsync("analyst", "wrong-password");

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUsername_ReturnsNull()
    {
        var repository = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var sut = CreateSut(repository, hasher);

        var result = await sut.LoginAsync("nobody", "anything");

        Assert.Null(result);
    }
}
