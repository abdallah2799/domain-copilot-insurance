using DomainCopilot.Domain.Identity;

namespace DomainCopilot.Domain.Tests.Identity;

public class UserTests
{
    [Fact]
    public void Create_WithValidInputs_SetsAllProperties()
    {
        var user = User.Create("adjuster", "hashed-password", UserRole.Adjuster);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("adjuster", user.Username);
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.Equal(UserRole.Adjuster, user.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankUsername_Throws(string username)
    {
        Assert.Throws<ArgumentException>(() => User.Create(username, "hashed-password", UserRole.Analyst));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankPasswordHash_Throws(string passwordHash)
    {
        Assert.Throws<ArgumentException>(() => User.Create("analyst", passwordHash, UserRole.Analyst));
    }

    [Fact]
    public void Create_AssignsDistinctIds()
    {
        var first = User.Create("analyst-1", "hash", UserRole.Analyst);
        var second = User.Create("analyst-2", "hash", UserRole.Analyst);

        Assert.NotEqual(first.Id, second.Id);
    }
}
