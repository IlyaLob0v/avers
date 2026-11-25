using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class WorkflowEntryTests
{
    [AvaloniaTheory]
    [InlineData("admin", true)]
    [InlineData("orlov", true)]
    [InlineData("engineer", false)]
    [InlineData("lawyer", false)]
    public void CashierControls_RespectReadOnlyRoles(string login, bool canRecord)
    {
        using var app = new DesktopApp(login);
        app.Go(AppModule.Abon);
        var payment = app.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && Equals(button.Content, "Внести оплату"));
        var amount = app.Window.GetVisualDescendants().OfType<StackPanel>()
            .Where(panel => panel.IsEffectivelyVisible && panel.Children.OfType<TextBlock>().Any(text => text.Text == "К внесению"))
            .SelectMany(panel => panel.Children.OfType<TextBox>()).Single();

        Assert.Equal(canRecord, payment.IsEnabled);
        Assert.Equal(!canRecord, amount.IsReadOnly);
    }

    [AvaloniaFact]
    public void CashierAtSmallSize_KeepsPaymentActionAndRowLabelsVisible()
    {
        using var app = new DesktopApp("orlov");
        app.Window.Width = 1000;
        app.Window.Height = 680;
        app.Go(AppModule.Abon);
        var paymentGrid = app.Window.GetVisualDescendants().OfType<DataGrid>()
            .Single(grid => grid.IsEffectivelyVisible && ReferenceEquals(grid.ItemsSource, app.Vm.PaymentRows));
        var payment = app.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && Equals(button.Content, "Внести оплату"));

        Assert.True(paymentGrid.Columns[0].ActualWidth >= 56, "Названия строк должны читаться целиком.");
        var bottom = payment.TranslatePoint(new Avalonia.Point(0, payment.Bounds.Height), app.Window);
        Assert.NotNull(bottom);
        Assert.InRange(bottom.Value.Y, 0, app.Window.Bounds.Height - 24);
    }

    [AvaloniaTheory]
    [InlineData("orlov", AppModule.Abon, "SubscriberSearch")]
    [InlineData("engineer", AppModule.Anten, "HouseRegistry")]
    public void Login_DoesNotChooseAnExampleRecord(string login, AppModule module, string visibleStage)
    {
        using var app = new DesktopApp(login, selectRecord: false);

        Assert.Equal(module, app.Vm.Module);
        Assert.Null(app.Vm.SelectedHouse);
        Assert.Null(app.Vm.SelectedSubscriber);
        Assert.Null(app.Vm.TicketsScreen.SelectedTicket);
        Assert.Empty(app.Vm.FindStreet);
        Assert.Empty(app.Vm.FindHouse);
        Assert.Empty(app.Vm.FindApartment);
        Assert.True(app.Window.GetVisualDescendants().OfType<Control>()
            .Single(control => control.Name == visibleStage).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task FindApartmentInCurrentHouse_UsesConfirmedHouseAfterSurnameSearch()
    {
        using var app = new DesktopApp("orlov");
        var houseId = app.Vm.SelectedHouse!.Id;
        var target = app.Vm.ContractsScreen.ContractRows.First(row => row.Subscriber.Id != app.Vm.SelectedSubscriber!.Id).Subscriber;
        app.Vm.SearchMode = SearchMode.Surname;
        app.Vm.FindQuery = "Другой запрос";
        app.Vm.FindStreet = "Другая улица";
        app.Vm.FindHouse = "999";
        app.Vm.FindSection = target.Section;
        app.Vm.FindApartment = target.Apartment;

        await app.Vm.FindApartmentInCurrentHouseCommand.ExecuteAsync(null);

        Assert.Equal(target.Id, app.Vm.SelectedSubscriber?.Id);
        Assert.Equal(houseId, app.Vm.SelectedHouse?.Id);
        Assert.Equal(SearchMode.Address, app.Vm.SearchMode);
    }
}
