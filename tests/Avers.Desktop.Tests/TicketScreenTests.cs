using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Data;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class TicketScreenTests
{
    private static readonly DateOnly Today = Avers.Data.DemoSeeder.WorkDate;

    [AvaloniaFact]
    public async Task NewTicket_EmptyDescription_DoesNotCreatePlaceholderTicket()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        var before = screen.Tickets.Count;
        screen.NewTicketFault = "   ";

        await screen.AcceptTicketCommand.ExecuteAsync(null);

        Assert.Equal(before, screen.Tickets.Count);
        Assert.Contains("неисправност", app.Vm.Status, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void Journal_FilterExcludesSelectedTicket_ClearsOldApartmentCard()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        Assert.NotNull(screen.SelectedTicket);

        screen.TicketSearch = "не существующая неисправность 987654321";
        app.Pump();

        Assert.Empty(screen.Tickets);
        Assert.Null(screen.SelectedTicket);
        Assert.False(screen.HasTicketSubscriber);
    }

    [AvaloniaFact]
    public async Task NewTicket_FormUsesExplicitApartmentAndMechanic_WithoutPreviousSubscriber()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        var create = app.Window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Новая заявка");
        create.Command!.Execute(null);
        app.Pump();
        var fields = app.Window.GetVisualDescendants().OfType<TextBox>().ToList();
        fields.Single(f => f.Name == "NewTicketSection").Text = "3";
        fields.Single(f => f.Name == "NewTicketApartment").Text = "999";
        fields.Single(f => f.Name == "NewTicketFault").Text = "Нет сигнала на всех каналах";
        fields.Single(f => f.Name == "NewTicketPhone").Text = "8-913-111-22-33";
        var mechanic = app.Window.GetVisualDescendants().OfType<AutoCompleteBox>()
            .Single(f => f.Name == "NewTicketMechanic");
        mechanic.Text = app.Vm.Mechanics[0].FullName;
        app.Pump();

        await screen.AcceptTicketCommand.ExecuteAsync(null);

        var saved = screen.Tickets.Single(t => t.Fault == "Нет сигнала на всех каналах");
        Assert.Equal(("3", "999", "8-913-111-22-33", app.Vm.Mechanics[0].FullName),
            (saved.Section, saved.Apartment, saved.Phone, saved.Mechanic));
        Assert.Null(saved.SubscriberId);
    }

    [AvaloniaFact]
    public async Task Journal_OpensSelectedTicket_WithoutPermanentApartmentPanel()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        screen.TicketSearch = "Иванова";
        app.Pump();
        var selected = screen.Tickets.First();
        screen.JournalSelectedTicket = selected;
        var open = app.Window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Открыть заявку");

        open.Command!.Execute(null);
        app.Pump();

        Assert.Equal(AppModule.Ticket, app.Vm.Module);
        Assert.Equal(selected.Id, screen.SelectedTicket!.Id);
        Assert.Equal("Иванова", screen.TicketSearch);
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<TextBlock>(),
            c => c.IsEffectivelyVisible && c.Text == "Квартира заявки");

        await app.Vm.BackCommand.ExecuteAsync(null);
        app.Pump();
        Assert.Equal(AppModule.Disp, app.Vm.Module);
        Assert.Equal("Иванова", screen.TicketSearch);
        Assert.Equal(selected.Id, screen.JournalSelectedTicket?.Id);
    }

    [AvaloniaFact]
    public async Task TicketCard_ChangingSelectedTicket_RequiresResolvingEdits()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Ticket);
        var screen = app.Vm.TicketsScreen;
        var previous = screen.SelectedTicket!;
        var originalFault = previous.Fault;
        var next = screen.Tickets.First(t => t.Id != previous.Id);
        screen.TicketFault = "Уточнённое описание со слов заявителя";

        screen.SelectedTicket = next;

        Assert.True(app.Vm.HasPendingEditTransition);
        Assert.Same(previous, screen.SelectedTicket);
        Assert.Equal("Уточнённое описание со слов заявителя", screen.TicketFault);
        await app.Vm.DiscardEditsAndContinueCommand.ExecuteAsync(null);
        Assert.Same(next, screen.SelectedTicket);
        Assert.Equal(originalFault, screen.Tickets.Single(t => t.Id == previous.Id).Fault);
    }

    [AvaloniaFact]
    public async Task TicketCard_BackCanSaveEditedDescription_BeforeReturningToJournal()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        var id = screen.SelectedTicket!.Id;
        app.Go(AppModule.Ticket);
        screen.TicketFault = "Новый текст неисправности";

        await app.Vm.BackCommand.ExecuteAsync(null);

        Assert.True(app.Vm.HasPendingEditTransition);
        Assert.Equal(AppModule.Ticket, app.Vm.Module);
        await app.Vm.SaveEditsAndContinueCommand.ExecuteAsync(null);
        Assert.Equal(AppModule.Disp, app.Vm.Module);
        Assert.Equal("Новый текст неисправности", screen.Tickets.Single(t => t.Id == id).Fault);
    }

    [AvaloniaFact]
    public void TicketCard_RefreshingSameRecord_PreservesEditsWithoutPrompt()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Ticket);
        var screen = app.Vm.TicketsScreen;
        var id = screen.SelectedTicket!.Id;
        var number = screen.SelectedTicket.Number;
        screen.TicketFault = "Редактируемый текст";

        screen.TicketSearch = number.ToString();
        app.Pump();

        Assert.Contains(screen.Tickets, t => t.Id == id);
        Assert.False(app.Vm.HasPendingEditTransition);
        Assert.Equal(id, screen.SelectedTicket!.Id);
        Assert.Equal("Редактируемый текст", screen.TicketFault);
    }

    [AvaloniaFact]
    public async Task CompletedTicket_LeavesOpenJournalSelectionEmpty_AfterSaveAndBack()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        screen.OnlyOpenTickets = true;
        screen.SelectedTicket = screen.Tickets.First(t => t.Status == TicketStatus.Accepted);
        var id = screen.SelectedTicket.Id;
        screen.OpenTicketCommand.Execute(null);
        screen.TicketState = screen.TicketStates.Single(state => state.Value == TicketStatus.Done);
        screen.TicketWork = "Сигнал восстановлен";
        screen.TicketMechanic = app.Vm.Mechanics[0].FullName;

        await screen.SaveTicketCommand.ExecuteAsync(null);

        Assert.Equal(AppModule.Disp, app.Vm.Module);
        Assert.DoesNotContain(screen.Tickets, ticket => ticket.Id == id);
        Assert.Null(screen.SelectedTicket);
        app.Go(AppModule.Desktop);
        await app.Vm.BackCommand.ExecuteAsync(null);
        Assert.Equal(AppModule.Disp, app.Vm.Module);
        Assert.Null(screen.SelectedTicket);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TicketCard_ClosingOpenTicket_DoesNotSelectInvisibleJournalRow(bool saveOnBack)
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        screen.OnlyOpenTickets = true;
        app.Pump();
        var id = screen.SelectedTicket!.Id;
        app.Go(AppModule.Ticket);
        screen.TicketWork = "Проверено, заменён разъём";

        if (saveOnBack)
        {
            await app.Vm.BackCommand.ExecuteAsync(null);
            Assert.True(app.Vm.HasPendingEditTransition);
            await app.Vm.SaveEditsAndContinueCommand.ExecuteAsync(null);
        }
        else
            await screen.SaveTicketCommand.ExecuteAsync(null);
        app.Pump();

        Assert.Equal(AppModule.Disp, app.Vm.Module);
        Assert.DoesNotContain(screen.Tickets, t => t.Id == id);
        Assert.Null(screen.SelectedTicket);
        Assert.Null(screen.JournalSelectedTicket);
        screen.OnlyOpenTickets = false;
        app.Pump();
        Assert.Equal(TicketStatus.Done, screen.Tickets.Single(t => t.Id == id).Status);
    }

    [AvaloniaFact]
    public async Task RepeatCall_ShowsOpenHouseTickets_AndOpensExistingTicket()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        screen.ShowNewTicketCommand.Execute(null);
        app.Pump();
        var button = app.Window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Проверить открытые заявки");
        Assert.Same(screen.CheckRepeatCallCommand, button.Command);

        await screen.CheckRepeatCallCommand.ExecuteAsync(null);

        Assert.NotEmpty(screen.RepeatCallTickets);
        Assert.All(screen.RepeatCallTickets, t =>
        {
            Assert.Equal(app.Vm.SelectedHouse!.Id, t.HouseId);
            Assert.NotEqual(TicketStatus.Done, t.Status);
        });
        screen.TicketYear = 2025;
        var existing = screen.RepeatCallTickets[0];
        screen.OpenRepeatCallTicketCommand.Execute(existing);
        Assert.Same(existing, screen.SelectedTicket);
        Assert.Equal(AppModule.Ticket, app.Vm.Module);
        app.Vm.FindApartment = "999";
        Assert.Empty(screen.RepeatCallTickets);
    }

    [AvaloniaFact]
    public void Journal_LoadsTicketsOfYear_ShowsRowsSummaryAndApartment()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;

        var grid = app.Window.GetVisualDescendants().OfType<DataGrid>().Single(g => g.IsEffectivelyVisible && ReferenceEquals(g.ItemsSource, screen.Tickets));
        var cells = grid.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains("Иванова М.П.", cells);
        Assert.Contains("Волочаевская", cells);
        Assert.NotEmpty(screen.Tickets);
        Assert.All(screen.Tickets, t => Assert.Equal(Today.Year, t.Date.Year));
        Assert.Equal([2026, 2025], screen.TicketYears);
        Assert.Equal("6 / 3", screen.TicketMonths.Single(m => m.Month == 8).Counts);

        Assert.Equal(DemoSeeder.StartTicket, screen.SelectedTicket!.Number);
        Assert.Equal("Иванова Мария Петровна", screen.TicketSubscriberName);
        Assert.StartsWith("№ ТО-2014/1932068 от 12.03.2014", screen.TicketContract);
        Assert.StartsWith("09.07.2026", screen.TicketLastPayment);
    }

    [AvaloniaFact]
    public void Journal_OtherYear_AskedFromStore()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;

        screen.TicketYear = 2025;
        app.Pump();

        Assert.NotEmpty(screen.Tickets);
        Assert.All(screen.Tickets, t => Assert.Equal(2025, t.Date.Year));
        Assert.Equal(screen.Tickets.Count, screen.TicketMonths.Sum(m => int.TryParse(m.Counts.Split(" / ")[0], out var n) ? n : 0));
    }

    [AvaloniaFact]
    public async Task SaveTicket_UpdatesJournal_WithoutReloadingSnapshot()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        var house = app.Vm.SelectedHouse;
        var street = app.Vm.Streets[0];

        screen.TicketWork = "Заменён штекер";
        await screen.SaveTicketCommand.ExecuteAsync(null);

        Assert.Equal("Заявка № 1001 записана.", app.Vm.Status);
        Assert.Equal(TicketStatus.Done, screen.Tickets.Single(t => t.Number == DemoSeeder.StartTicket).Status);
        Assert.Equal(TicketStatus.Done, screen.SelectedTicket!.Status);
        Assert.Equal("6 / 4", screen.TicketMonths.Single(m => m.Month == 8).Counts);
        Assert.Same(house, app.Vm.SelectedHouse);
        Assert.Same(street, app.Vm.Streets[0]);
    }

    [AvaloniaFact]
    public async Task Dispatcher_DefersThenClosesWithDate_PhoneFromCall()
    {
        using var app = new DesktopApp("orlova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        screen.NewTicketFault = "Нет сигнала";
        screen.NewTicketPhone = "8-913-000-00-00";
        screen.AcceptTicketCommand.Execute(null);
        app.Pump();
        var number = screen.SelectedTicket!.Number;
        Assert.Equal("8-913-000-00-00", screen.SelectedTicket.Phone);

        screen.TicketWork = "Нет доступа на техэтаж";
        Assert.Equal(TicketStatus.Done, screen.TicketState?.Value);
        screen.TicketState = screen.TicketStates.Single(s => s.Value == TicketStatus.Deferred);
        await screen.SaveTicketCommand.ExecuteAsync(null);
        var ticket = screen.Tickets.Single(t => t.Number == number);
        Assert.Equal((TicketStatus.Deferred, (DateOnly?)null, "Нет доступа на техэтаж"), (ticket.Status, ticket.DoneDate, ticket.WorkDone));

        screen.SelectedTicket = ticket;
        screen.TicketState = screen.TicketStates.Single(s => s.Value == TicketStatus.Done);
        Assert.Equal(Today.ToString("dd.MM.yyyy"), screen.TicketDoneDate);
        screen.TicketDoneDate = Today.AddDays(1).ToString("dd.MM.yyyy");
        await screen.SaveTicketCommand.ExecuteAsync(null);
        Assert.Contains("Дата выполнения", app.Vm.Status);
        Assert.Equal(TicketStatus.Deferred, screen.Tickets.Single(t => t.Number == number).Status);

        screen.TicketDoneDate = Today.ToString("dd.MM.yyyy");
        screen.TicketMechanic = app.Vm.Mechanics[0].FullName;
        await screen.SaveTicketCommand.ExecuteAsync(null);
        ticket = screen.Tickets.Single(t => t.Number == number);
        Assert.Equal((TicketStatus.Done, (DateOnly?)Today, app.Vm.Mechanics[0].FullName), (ticket.Status, ticket.DoneDate, ticket.Mechanic));
        Assert.Contains(app.Vm.Mechanics[0].FullName, screen.MechanicNames);
    }

    [AvaloniaFact]
    public void Overdue_RedInJournal_FilterAndDueDate()
    {
        var today = Today.AddDays(TicketJournal.DueDays + 1);
        using var app = new DesktopApp("orlova", today);
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        screen.PeriodAllCommand.Execute(null);
        var overdue = screen.Tickets.ToList().Where(t => TicketJournal.IsOverdue(t, today)).ToList();
        Assert.NotEmpty(overdue);
        Assert.Equal(Controls.RowTone.Debt, screen.TicketTone(overdue[0]));
        Assert.Contains($"просрочено {overdue.Count}", screen.TicketCountText);

        screen.OnlyOverdueTickets = true;

        Assert.Equal(overdue.Select(t => t.Number).Order(), screen.Tickets.Select(t => t.Number).Order());
        screen.SelectedTicket = screen.Tickets[0];
        Assert.EndsWith(", просрочена", screen.TicketDueText);

        screen.OnlyOverdueTickets = false;
        screen.NewTicketFault = "Нет сигнала";
        screen.AcceptTicketCommand.Execute(null);
        app.Pump();
        Assert.Equal($"до {today.AddDays(TicketJournal.DueDays):dd.MM.yyyy}", screen.TicketDueText);
        Assert.Equal(Controls.RowTone.None, screen.TicketTone(screen.SelectedTicket));
    }

    [AvaloniaFact]
    public void Oek_ExportsDayWhenAllDone_HiddenOtherwise()
    {
        using var app = new DesktopApp("orlova");
        app.Go(AppModule.Disp);
        var screen = app.Vm.TicketsScreen;
        string? opened = null;
        app.Vm.OpenFile = path => opened = path;
        screen.PeriodAllCommand.Execute(null);
        var doneDay = screen.Tickets.ToList().GroupBy(t => t.Date).First(g => g.All(t => t.Status == TicketStatus.Done));
        var openDay = screen.Tickets.ToList().GroupBy(t => t.Date).First(g => g.Any(t => t.Status != TicketStatus.Done));

        screen.OekDate = openDay.Key.ToDateTime(TimeOnly.MinValue);
        screen.ExportOekCommand.Execute(null);
        Assert.Null(opened);
        Assert.Contains("скрыт", app.Vm.Status);

        screen.OekDate = doneDay.Key.ToDateTime(TimeOnly.MinValue);
        screen.ExportOekCommand.Execute(null);
        using var book = new ClosedXML.Excel.XLWorkbook(opened!);
        Assert.Equal(doneDay.Count(), book.Worksheet(1).RowsUsed().Count() - 2);
    }
}
