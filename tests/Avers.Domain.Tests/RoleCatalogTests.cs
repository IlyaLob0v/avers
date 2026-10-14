using Avers.Domain;
using AccessLevel = Avers.Domain.RoleCatalog.AccessLevel;

namespace Avers.Domain.Tests;

public class RoleCatalogTests
{
    [Fact]
    public void EveryRoleHasTitle()
    {
        foreach (var role in Enum.GetValues<UserRole>())
            Assert.False(string.IsNullOrWhiteSpace(RoleCatalog.Title(role)));
    }

    [Theory]
    [InlineData(UserRole.Administrator, AppModule.Abon, AccessLevel.Edit)]
    [InlineData(UserRole.ChiefEngineer, AppModule.Anten, AccessLevel.Edit)]
    [InlineData(UserRole.ChiefEngineer, AppModule.Abon, AccessLevel.View)]
    [InlineData(UserRole.Dispatcher, AppModule.Disp, AccessLevel.Edit)]
    [InlineData(UserRole.Lawyer, AppModule.Legal, AccessLevel.Edit)]
    [InlineData(UserRole.Reader, AppModule.Scheme, AccessLevel.ScreenOnly)]
    public void Can_GrantsExpectedLevel(UserRole role, AppModule module, AccessLevel expected)
    {
        Assert.True(RoleCatalog.Can(role, module, out var level));
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData(UserRole.Dispatcher, AppModule.Abon)]
    [InlineData(UserRole.Dispatcher, AppModule.Legal)]
    [InlineData(UserRole.Lawyer, AppModule.Disp)]
    [InlineData(UserRole.Reader, AppModule.Abon)]
    [InlineData(UserRole.Reader, AppModule.Legal)]
    public void Can_DeniesModulesOutsideRole(UserRole role, AppModule module)
    {
        Assert.False(RoleCatalog.Can(role, module, out var level));
        Assert.Equal(AccessLevel.None, level);
    }

    [Fact]
    public void Reader_CannotEditAnything()
    {
        foreach (var module in Enum.GetValues<AppModule>())
        {
            RoleCatalog.Can(UserRole.Reader, module, out var level);
            Assert.NotEqual(AccessLevel.Edit, level);
        }
    }
}
