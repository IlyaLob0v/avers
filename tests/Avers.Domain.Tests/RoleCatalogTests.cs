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
    [InlineData(UserRole.Dispatcher, AppModule.Abon, AccessLevel.View)]
    [InlineData(UserRole.AdsHead, AppModule.Disp, AccessLevel.Edit)]
    [InlineData(UserRole.Lawyer, AppModule.Legal, AccessLevel.Edit)]
    [InlineData(UserRole.Reader, AppModule.Scheme, AccessLevel.ScreenOnly)]
    public void Can_GrantsExpectedLevel(UserRole role, AppModule module, AccessLevel expected)
    {
        Assert.True(RoleCatalog.Can(role, module, out var level));
        Assert.Equal(expected, level);
    }

    [Theory]
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

    [Fact]
    public void AdsRoles_AreHeadAndUser()
    {
        Assert.Equal("Начальник АДС", RoleCatalog.Title(UserRole.AdsHead));
        Assert.StartsWith("Пользователь АДС", RoleCatalog.Title(UserRole.Dispatcher));
    }

    [Theory]
    [InlineData(UserRole.AdsHead, 1, true)]
    [InlineData(UserRole.AdsHead, 2, true)]
    [InlineData(UserRole.Administrator, 2, true)]
    [InlineData(UserRole.Dispatcher, 1, true)]
    [InlineData(UserRole.Dispatcher, 2, false)]
    [InlineData(UserRole.ChiefEngineer, 1, false)]
    [InlineData(UserRole.Reader, 1, false)]
    public void CanEditTicket_UserAdsEditsOnlyOwn(UserRole role, int userId, bool expected)
    {
        var ticket = new WorkTicket { AcceptedByUserId = 1 };

        Assert.Equal(expected, RoleCatalog.CanEditTicket(role, userId, ticket));
    }

    [Fact]
    public void CanEditTicket_DispatcherCannotEditTicketWithoutAuthor()
    {
        Assert.False(RoleCatalog.CanEditTicket(UserRole.Dispatcher, 1, new WorkTicket()));
        Assert.True(RoleCatalog.CanEditTicket(UserRole.AdsHead, 1, new WorkTicket()));
    }
}
