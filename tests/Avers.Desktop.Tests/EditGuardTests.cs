using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Domain;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.Tests;

public class EditGuardTests
{
    [AvaloniaFact]
    public async Task PendingPayment_StayKeepsAmountWithoutSavingIt()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Abon);
        var subscriberId = app.Vm.SelectedSubscriber!.Id;
        app.Vm.PaymentAmount = "250,50";

        app.Vm.ShowSubscriberSearchCommand.Execute(null);
        app.Pump();

        Assert.Equal(subscriberId, app.Vm.SelectedSubscriber?.Id);
        Assert.True(app.Vm.HasPendingEditTransition);
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<Button>(),
            button => Equals(button.Content, "Сохранить изменения") && button.IsEffectivelyVisible);
        await Press(app, "Остаться");
        Assert.Equal("250,50", app.Vm.PaymentAmount);
        Assert.Equal(subscriberId, app.Vm.SelectedSubscriber?.Id);
    }

    [AvaloniaFact]
    public async Task PendingPayment_DiscardContinuesWithoutRecordingPayment()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Abon);
        var subscriber = app.Vm.SelectedSubscriber!;
        var paidBefore = subscriber.Payments.Count(payment => payment.Amount is not null);
        app.Vm.PaymentAmount = "250,50";

        app.Vm.ShowSubscriberSearchCommand.Execute(null);
        app.Pump();
        Assert.True(app.Vm.HasPendingEditTransition);
        await Press(app, "Отменить изменения");
        Assert.Null(app.Vm.SelectedSubscriber);
        Assert.Empty(app.Vm.PaymentAmount);
        app.Vm.SearchMode = SearchMode.Account;
        app.Vm.FindQuery = subscriber.Account;
        await app.Vm.FindCommand.ExecuteAsync(null);
        Assert.Equal(paidBefore, app.Vm.SelectedSubscriber!.Payments.Count(payment => payment.Amount is not null));
    }

    [AvaloniaFact]
    public async Task Payment_WithUnsavedCard_DoesNotRecordOrEraseDraft()
    {
        using var app = new DesktopApp();
        var paidBefore = app.Vm.SelectedSubscriber!.Payments.Count(payment => payment.Amount is not null);
        app.Vm.EditNotes = "Несохранённое примечание";
        app.Vm.PaymentAmount = "250,50";

        await app.Vm.RecordPaymentCommand.ExecuteAsync(null);

        Assert.Equal(paidBefore, app.Vm.SelectedSubscriber!.Payments.Count(payment => payment.Amount is not null));
        Assert.Equal("Несохранённое примечание", app.Vm.EditNotes);
        Assert.Equal("250,50", app.Vm.PaymentAmount);
    }

    [AvaloniaFact]
    public async Task NewSearch_WithChangedCard_StaysUntilEmployeeDecides()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Subscriber);
        var subscriber = app.Vm.SelectedSubscriber;
        app.Vm.EditNotes = "Несохранённое примечание";

        app.Vm.ShowSubscriberSearchCommand.Execute(null);
        app.Pump();

        Assert.Equal(AppModule.Subscriber, app.Vm.Module);
        Assert.Same(subscriber, app.Vm.SelectedSubscriber);
        await Press(app, "Остаться");
        Assert.Equal("Несохранённое примечание", app.Vm.EditNotes);
        Assert.Same(subscriber, app.Vm.SelectedSubscriber);
        app.Vm.EditNotes = subscriber!.Notes;
    }

    [AvaloniaFact]
    public async Task SaveWithInvalidDate_DoesNotContinuePendingNewSearch()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Subscriber);
        var subscriber = app.Vm.SelectedSubscriber;
        app.Vm.EditContractDate = "не дата";

        app.Vm.ShowSubscriberSearchCommand.Execute(null);
        app.Pump();
        Assert.Equal(AppModule.Subscriber, app.Vm.Module);
        await Press(app, "Сохранить изменения");

        Assert.Equal(AppModule.Subscriber, app.Vm.Module);
        Assert.Same(subscriber, app.Vm.SelectedSubscriber);
        Assert.Equal("не дата", app.Vm.EditContractDate);
        Assert.Contains("Дата договора", app.Vm.Status);
        await Press(app, "Отменить изменения");
        Assert.Equal(AppModule.Abon, app.Vm.Module);
        Assert.Null(app.Vm.SelectedSubscriber);
    }

    [AvaloniaFact]
    public async Task SaveWithConcurrentHouseChange_DoesNotContinuePendingNavigation()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Anten);
        var house = app.Vm.SelectedHouse!;
        house.OtherNotes = "Не потерять при конфликте";
        house.Revision++;

        app.Vm.OpenModuleCommand.Execute(AppModule.Desktop);
        app.Pump();
        Assert.Equal(AppModule.Anten, app.Vm.Module);
        await Press(app, "Сохранить изменения");

        Assert.Equal(AppModule.Anten, app.Vm.Module);
        Assert.Same(house, app.Vm.SelectedHouse);
        Assert.Equal("Не потерять при конфликте", house.OtherNotes);
        Assert.Contains("изменена другим сотрудником", app.Vm.Status);
        await Press(app, "Отменить изменения");
    }

    [AvaloniaFact]
    public async Task DiscardHouse_RestoresHouseAndSharedOrganizationBeforeLeaving()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Anten);
        var house = app.Vm.SelectedHouse!;
        var originalNotes = house.OtherNotes;
        var originalOrganization = house.Organization!.Name;
        house.OtherNotes = "Не сохранять";
        house.Organization.Name = "Временное название";

        app.Vm.OpenModuleCommand.Execute(AppModule.Desktop);
        app.Pump();
        Assert.Equal(AppModule.Anten, app.Vm.Module);
        await Press(app, "Отменить изменения");

        Assert.Equal(AppModule.Desktop, app.Vm.Module);
        Assert.Equal(originalNotes, house.OtherNotes);
        Assert.Equal(originalOrganization, house.Organization.Name);
    }

    [AvaloniaFact]
    public async Task SaveCard_SavesBeforeStartingNewSearch()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Subscriber);
        var account = app.Vm.SelectedSubscriber!.Account;
        app.Vm.EditNotes = "Сохранённое примечание";

        app.Vm.ShowSubscriberSearchCommand.Execute(null);
        app.Pump();
        Assert.Equal(AppModule.Subscriber, app.Vm.Module);
        await Press(app, "Сохранить изменения");

        Assert.Equal(AppModule.Abon, app.Vm.Module);
        Assert.Null(app.Vm.SelectedSubscriber);
        app.Vm.SearchMode = SearchMode.Account;
        app.Vm.FindQuery = account;
        await app.Vm.FindCommand.ExecuteAsync(null);
        Assert.Equal("Сохранённое примечание", app.Vm.SelectedSubscriber!.Notes);
    }

    [AvaloniaFact]
    public async Task CloseWithChangedCard_CanStayInTheOpenWindow()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Subscriber);
        var originalNotes = app.Vm.EditNotes;
        app.Vm.EditNotes = "Не закрывать";

        app.Window.Close();
        app.Pump();

        Assert.True(app.Window.IsVisible);
        await Press(app, "Остаться");
        Assert.True(app.Window.IsVisible);
        app.Vm.EditNotes = originalNotes;
    }

    private static async Task Press(DesktopApp app, string content)
    {
        var button = app.Window.GetVisualDescendants().OfType<Button>()
            .Single(control => control.Content as string == content && control.IsEffectivelyVisible);
        Assert.NotNull(button.Command);
        if (button.Command is IAsyncRelayCommand asyncCommand)
            await asyncCommand.ExecuteAsync(button.CommandParameter);
        else
            button.Command.Execute(button.CommandParameter);
        app.Pump();
    }
}
