using Avers.Domain;

namespace Avers.Data.Tests;

public class PasswordTests
{
    [Fact]
    public void Hash_IsStableAndHex()
    {
        var hash = Password.Hash("admin", "avers");

        Assert.Equal(hash, Password.Hash("admin", "avers"));
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-F]+$", hash);
    }

    [Fact]
    public void Hash_IgnoresLoginCase()
    {
        Assert.Equal(Password.Hash("Admin", "avers"), Password.Hash("admin", "avers"));
    }

    [Fact]
    public void Hash_DependsOnPasswordCase()
    {
        Assert.NotEqual(Password.Hash("admin", "Avers"), Password.Hash("admin", "avers"));
    }

    [Fact]
    public void Verify_ChecksAgainstStoredHash()
    {
        var user = new AppUser { Login = "orlov", PasswordHash = Password.Hash("orlov", "secret") };

        Assert.True(Password.Verify(user, "secret"));
        Assert.False(Password.Verify(user, "Secret"));
    }
}
