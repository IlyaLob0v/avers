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
