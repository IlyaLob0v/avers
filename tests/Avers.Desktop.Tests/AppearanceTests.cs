using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avers.Data;
using Avers.Desktop.ViewModels;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class AppearanceTests
{
    [AvaloniaTheory]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(600)]
    [InlineData(700)]
    public void UiFont_IsEmbeddedPlexWithRealWeights(int weight)
    {
        var family = (FontFamily)Application.Current!.FindResource("UiFont")!;
        var typeface = new Typeface(family, FontStyle.Normal, (FontWeight)weight);

        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var face));
        Assert.StartsWith("IBM Plex Sans", face.FamilyName);
        Assert.Equal((FontWeight)weight, face.Weight);
        Assert.Equal(FontSimulations.None, face.FontSimulations);
    }

    [Fact]
    public void Catalog_EveryModuleHasTileSummary()
    {
        Assert.All(NavCatalog.All, e => Assert.False(string.IsNullOrWhiteSpace(e.Summary)));
    }

    [AvaloniaTheory]
    [InlineData("admin")]
    [InlineData("orlova")]
    public void Desktop_ShowsTileForEveryAvailableModule(string login)
    {
        using var app = new DesktopApp(login);
        app.Go(AppModule.Desktop);

        var tiles = app.Window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("tile") && b.IsEffectivelyVisible)
            .Select(b => (NavItem)b.DataContext!)
            .ToList();

        Assert.Equal(app.Vm.Navigation.Select(n => n.Module), tiles.Select(t => t.Module));
    }

    [AvaloniaFact]
    public void Abon_DebtTotal_IsRedOnlyWhenThereIsDebt()
    {
        using var app = new DesktopApp("orlov");
        app.Go(AppModule.Abon);
        app.Vm.ContractsScreen.OnlyActiveContracts = false;
        var subscribers = new List<Subscriber>();
        foreach (var street in app.Vm.Streets.ToList())
        {
            app.Vm.SelectedStreet = street;
            foreach (var house in app.Vm.HousesOnStreet.ToList())
            {
                app.Vm.SelectedHouse = house;
                subscribers.AddRange(app.Vm.ContractsScreen.ContractRows.Select(r => r.Subscriber));
            }
        }

        foreach (var subscriber in subscribers)
        {
            app.Vm.SelectedSubscriber = subscriber;
            Assert.Equal(HasDebt(subscriber), app.Vm.HasDebt);
        }
        Assert.Contains(subscribers, HasDebt);
        Assert.Contains(subscribers, s => !HasDebt(s));

        app.Vm.SelectedSubscriber = null;
        Assert.False(app.Vm.HasDebt);
    }

    private static bool HasDebt(Subscriber s) => Billing.Debt(Billing.Due(s.Payments, DemoSeeder.WorkDate)) > 0;

    [AvaloniaFact]
    public void Shell_ShowsUserAndRoleInHeader()
    {
        using var app = new DesktopApp("orlova");

        Assert.False(string.IsNullOrEmpty(app.Vm.UserRoleTitle));
        Assert.Equal($"{app.Vm.User!.DisplayName} · {app.Vm.UserRoleTitle}", app.Vm.UserLine);
        Assert.Contains(app.Window.GetVisualDescendants().OfType<TextBlock>(),
            t => t.IsEffectivelyVisible && t.Inlines is { } inlines && inlines.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text == app.Vm.UserRoleTitle));
    }
}
