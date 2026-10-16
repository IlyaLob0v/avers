using System.Security.Cryptography;
using System.Text;
using Avers.Domain;

namespace Avers.Data.Tests;

public class PasswordTests
{
    [Fact]
    public void Hash_IsSaltedAndVerifies()
    {
        var first = Password.Hash("secret");
        var second = Password.Hash("secret");

        Assert.NotEqual(first, second);
        Assert.StartsWith("pbkdf2.", first);
        Assert.True(Password.Verify(new AppUser { Login = "orlov", PasswordHash = first }, "secret"));
        Assert.True(Password.Verify(new AppUser { Login = "orlov", PasswordHash = second }, "secret"));
    }

    [Fact]
    public void Verify_DependsOnPasswordCase()
    {
        var user = new AppUser { Login = "orlov", PasswordHash = Password.Hash("secret") };

        Assert.False(Password.Verify(user, "Secret"));
        Assert.False(Password.NeedsRehash(user.PasswordHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pbkdf2.x.y.z")]
    [InlineData("pbkdf2.1000.!!!.???")]
    [InlineData("md5.1.AA==.AA==")]
    public void Verify_RejectsBrokenHash(string stored)
    {
        Assert.False(Password.Verify(new AppUser { Login = "orlov", PasswordHash = stored }, "secret"));
    }

    [Fact]
    public void LegacyHash_StillAcceptedButNeedsRehash()
    {
        var legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("orlov:secret:avers")));
        var user = new AppUser { Login = "Orlov", PasswordHash = legacy };

        Assert.True(Password.Verify(user, "secret"));
        Assert.False(Password.Verify(user, "wrong"));
        Assert.True(Password.NeedsRehash(legacy));
    }
}
