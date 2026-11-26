using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class PprFilterTests
{
    [AvaloniaFact]
    public void OrganizationAndZone_ClickAndSelectApplyTheirOwnFilter()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Ppr);
        var filter = app.Window.GetVisualDescendants().OfType<ComboBox>()
            .Single(box => box.IsEffectivelyVisible && ReferenceEquals(box.ItemsSource, app.Vm.PprScreen.PprFilters));
        var allHouses = app.Vm.PprScreen.PprRows.Select(row => row.House).ToArray();
        var organization = allHouses.First().Organization!.Name;
        filter.SelectedItem = organization;
        app.Pump();
        Assert.NotEmpty(app.Vm.PprScreen.PprRows);
        Assert.All(app.Vm.PprScreen.PprRows, row => Assert.Equal(organization, row.House.Organization!.Name));

        var zoneButton = app.Window.GetVisualDescendants().OfType<RadioButton>()
            .Single(button => button.IsEffectivelyVisible && Equals(button.Content, "зоне"));
        Click(app, zoneButton);
        Assert.True(app.Vm.PprScreen.PprByZone);
        Assert.Contains(allHouses.First().Zone, filter.ItemsSource!.Cast<string>());
        Assert.DoesNotContain(organization, filter.ItemsSource!.Cast<string>());
        Assert.Equal(allHouses.Length, app.Vm.PprScreen.PprRows.Count);
        Assert.Contains(app.Window.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == "Зона");
        var selectedZone = allHouses.First().Zone;
        filter.SelectedItem = selectedZone;
        app.Pump();
        Assert.NotEmpty(app.Vm.PprScreen.PprRows);
        Assert.All(app.Vm.PprScreen.PprRows, row => Assert.Equal(selectedZone, row.House.Zone));
        Assert.Equal(app.Vm.PprScreen.PprRows.Count(row => row.Cells.Any(cell => cell.Status != PprStatus.Empty)),
            app.Vm.PprScreen.PprTotals[0].Year);

        var organizationButton = app.Window.GetVisualDescendants().OfType<RadioButton>()
            .Single(button => button.IsEffectivelyVisible && Equals(button.Content, "обслуживающей организации"));
        Click(app, organizationButton);
        Assert.False(app.Vm.PprScreen.PprByZone);
        filter.SelectedItem = organization;
        app.Pump();
        Assert.All(app.Vm.PprScreen.PprRows, row => Assert.Equal(organization, row.House.Organization!.Name));
    }

    private static void Click(DesktopApp app, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), app.Window);
        Assert.NotNull(point);
        app.Window.MouseDown(point.Value, MouseButton.Left);
        app.Window.MouseUp(point.Value, MouseButton.Left);
        app.Pump();
    }
}
