using DomainCopilot.Infrastructure.Identity;

namespace DomainCopilot.Contract.Tests;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _sut = new();

    [Fact]
    public void Hash_ThenVerify_WithCorrectPassword_Succeeds()
    {
        var hash = _sut.Hash("correct-password");

        Assert.True(_sut.Verify("correct-password", hash));
    }

    [Fact]
    public void Verify_WithWrongPassword_Fails()
    {
        var hash = _sut.Hash("correct-password");

        Assert.False(_sut.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_ProducesADifferentValueEachTime_BecauseOfRandomSalt()
    {
        var first = _sut.Hash("same-password");
        var second = _sut.Hash("same-password");

        Assert.NotEqual(first, second);
        Assert.True(_sut.Verify("same-password", first));
        Assert.True(_sut.Verify("same-password", second));
    }

    [Theory]
    [InlineData("not-a-valid-hash")]
    [InlineData("1.2")]
    [InlineData("not-a-number.c2FsdA==.c3Via2V5")]
    public void Verify_WithMalformedStoredHash_ReturnsFalseRatherThanThrowing(string malformedHash)
    {
        Assert.False(_sut.Verify("anything", malformedHash));
    }
}
