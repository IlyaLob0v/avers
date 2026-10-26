using Avalonia.Headless.XUnit;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class TicketScreenTests
{
    private static readonly DateOnly Today = Avers.Data.DemoSeeder.WorkDate;

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
}
