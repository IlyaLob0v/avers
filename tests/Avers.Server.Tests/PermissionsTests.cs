using Avers.Domain;
using Avers.Server.Auth;
using AccessLevel = Avers.Domain.RoleCatalog.AccessLevel;

namespace Avers.Server.Tests;

public class PermissionsTests
{
    [Fact]
    public void ScreenOnly_IsNotView()
    {
        // Читателю схемы только на экране: проверку «просмотр» (скачать исходный файл) он не проходит.
        Assert.False(Policies.HasAccess(UserRole.Reader, AppModule.Scheme, AccessLevel.View));
        Assert.True(Policies.HasAccess(UserRole.Reader, AppModule.Scheme, AccessLevel.ScreenOnly));
    }

    [Theory]
    [InlineData(UserRole.ChiefEngineer)]
    [InlineData(UserRole.Dispatcher)]
    public void ScreenOnly_IsGrantedToViewAndEdit(UserRole role)
    {
        Assert.True(Policies.HasAccess(role, AppModule.Scheme, AccessLevel.View));
        Assert.True(Policies.HasAccess(role, AppModule.Scheme, AccessLevel.ScreenOnly));
    }
}
