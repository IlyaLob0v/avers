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

    [AvaloniaFact]
    public void DefaultFont_IsPlex_SoPopupsMatchWindows()
    {
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(FontFamily.Default), out var face));
        Assert.StartsWith("IBM Plex Sans", face.FamilyName);
    }

    [AvaloniaFact]
    public void DesktopTile_HasColorTransitionsUnlessMotionIsReduced()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Desktop);

        var tile = app.Window.GetVisualDescendants().OfType<Button>()
            .First(b => b.Classes.Contains("tile") && b.IsEffectivelyVisible);
        var presenter = tile.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
            .First(p => p.Name == "PART_ContentPresenter");
        var transitions = presenter.Transitions?.OfType<Avalonia.Animation.BrushTransition>().ToArray() ?? [];
        if (Environment.GetEnvironmentVariable("AVERS_REDUCE_MOTION") == "1")
            Assert.Empty(transitions);
        else
            Assert.Equal(2, transitions.Length);
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

        Assert.Equal(app.Vm.NavigationGroups.SelectMany(group => group.Items).Select(n => n.Module), tiles.Select(t => t.Module));
    }

    [AvaloniaFact]
    public async Task Abon_DebtTotal_IsRedOnlyWhenThereIsDebt()
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

        var observed = new List<bool>();
        foreach (var subscriber in subscribers)
        {
            app.Vm.SearchMode = SearchMode.Account;
            app.Vm.FindQuery = subscriber.Account;
            await app.Vm.FindCommand.ExecuteAsync(null);
            Assert.Equal(subscriber.Id, app.Vm.SelectedSubscriber!.Id);
            var expected = HasDebt(app.Vm.SelectedSubscriber);
            Assert.Equal(expected, app.Vm.HasDebt);
            observed.Add(expected);
        }
        Assert.Contains(true, observed);
        Assert.Contains(false, observed);

        app.Vm.SelectedSubscriber = null;
        Assert.False(app.Vm.HasDebt);
    }

    private static bool HasDebt(Subscriber s) => Billing.Debt(Billing.Due(s.Payments, DemoSeeder.WorkDate)) > 0;

    [AvaloniaTheory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public void OriginalArchive_ExpandedTableShowsSourceIdDateAndAmount(string cultureName)
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        var previousUiCulture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
        try
        {
            using var app = new DesktopApp();
            app.Go(AppModule.Subscriber);
            app.Vm.ArchiveScreen.IsExpanded = true;
            var row = new JackPayment
            {
                Id = 3_000_000_000,
                SubscriberId = app.Vm.SelectedSubscriber!.Id,
                Period = new DateTime(1996, 1, 17, 16, 45, 20),
                PaidAt = new DateTime(1996, 2, 3, 10, 25, 37),
                Amount = 15000.25m,
                Source = "Проверка архива"
            };
            app.Vm.ArchiveScreen.Rows.Add(row);
            app.Pump();
            var grid = app.Window.GetVisualDescendants().OfType<DataGrid>()
                .Single(g => ReferenceEquals(g.ItemsSource, app.Vm.ArchiveScreen.Rows));
            grid.BringIntoView();
            app.Pump();
            var texts = grid.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("3000000000", texts);
            Assert.Contains("01.1996", texts);
            Assert.Contains("03.02.1996 10:25", texts);
            Assert.Contains(15000.25m.ToString("N2", System.Globalization.CultureInfo.CurrentCulture), texts);
            Assert.Equal(3_000_000_000, row.Id);
            Assert.Equal(new DateTime(1996, 1, 17, 16, 45, 20), row.Period);
            Assert.Equal(new DateTime(1996, 2, 3, 10, 25, 37), row.PaidAt);
            Assert.Equal(15000.25m, row.Amount);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previousCulture;
            System.Globalization.CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

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
