using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Data;
using Avers.Desktop.Controls;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class ShellTests
{
    [AvaloniaTheory]
    [InlineData("admin")]
    [InlineData("engineer")]
    [InlineData("sokolova")]
    [InlineData("orlov")]
    [InlineData("lawyer")]
    [InlineData("novikov")]
    public void EveryModule_OpensForEveryRole(string login)
    {
        using var app = new DesktopApp(login);

        foreach (var module in Enum.GetValues<AppModule>())
        {
            app.Go(module);
            Assert.Equal(module, app.Vm.Module);
            Assert.NotNull(app.Window.CaptureRenderedFrame());
        }
    }

    [AvaloniaFact]
    public void MockupButton_SaysItIsNotReady()
    {
        using var app = new DesktopApp();

        app.Vm.NotReadyCommand.Execute("Печать");

        Assert.Equal("«Печать» пока не работает: функция в разработке.", app.Vm.Status);
    }

    [AvaloniaFact]
    public void Journal_SelectingAnotherHouse_KeepsIt()
    {
        using var app = new DesktopApp("orlov");
        app.Go(AppModule.Disp);
        var other = app.Vm.HousesOnStreet.First(h => h.Id != app.Vm.SelectedHouse!.Id);

        app.Vm.SelectedHouse = other;
        app.Pump();

        Assert.Same(other, app.Vm.SelectedHouse);
        Assert.Contains(app.Vm.HousesOnStreet, h => h.Id == other.Id);
    }

    [AvaloniaTheory]
    [InlineData("orlov", true)]     // начальник АДС
    [InlineData("sokolova", true)]  // пользователь АДС
    [InlineData("engineer", false)] // главный инженер только смотрит
    public void Journal_AcceptButton_EnabledRightAfterLogin(string login, bool enabled)
    {
        using var app = new DesktopApp(login);
        app.Go(AppModule.Disp);

        var accept = app.Window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.IsEffectivelyVisible && Equals(b.Content, "Принять заявку"));
        Assert.Equal(enabled, accept.IsEnabled);
    }

    [AvaloniaFact]
    public void RecordPayment_StaysOnTheSameApartment()
    {
        using var app = new DesktopApp("orlov");
        app.Go(AppModule.Abon);
        var subscriber = app.Vm.SelectedSubscriber!;
        var paidBefore = subscriber.Payments.Count(p => p.Amount is not null);

        app.Vm.RecordPaymentCommand.Execute(null);
        app.Pump();

        Assert.Equal(subscriber.Id, app.Vm.SelectedSubscriber!.Id);
        Assert.Equal(subscriber.HouseId, app.Vm.SelectedHouse!.Id);
        Assert.Equal(paidBefore + 1, app.Vm.SelectedSubscriber.Payments.Count(p => p.Amount is not null));
        Assert.Equal("Орлов С.А.", app.Vm.SelectedSubscriber.Payments.Where(p => p.PaidAt == DemoSeeder.WorkDate).Single().RecordedBy);
        Assert.Contains($"{DemoSeeder.WorkDate.Year}", app.Vm.Status);
    }

    [AvaloniaFact]
    public void Dispatcher_CannotRecordPayment()
    {
        using var app = new DesktopApp("sokolova");
        var paidBefore = app.Vm.SelectedSubscriber!.Payments.Count(p => p.Amount is not null);

        app.Vm.RecordPaymentCommand.Execute(null);

        Assert.Equal("Нет права вносить оплату.", app.Vm.Status);
        Assert.Equal(paidBefore, app.Vm.SelectedSubscriber!.Payments.Count(p => p.Amount is not null));
    }

    [AvaloniaFact]
    public void Debtors_AreColored()
    {
        using var app = new DesktopApp("lawyer");

        Assert.NotEmpty(app.Vm.Debtors);
        Assert.All(app.Vm.Debtors, d => Assert.Equal(d.IsDebt ? RowTone.Debt : RowTone.Warn, d.Tone));
    }

    [AvaloniaFact]
    public void Claim_NamesUnpaidMonthsOfSelectedDebtor()
    {
        using var app = new DesktopApp("lawyer");

        foreach (var debtor in app.Vm.Debtors.ToList())
        {
            app.Vm.SelectedDebtor = debtor;
            Assert.Contains($"за {debtor.Unpaid} мес.", app.Vm.ClaimText);
        }
    }

    [AvaloniaFact]
    public void Report_CountsDoneTicketsOfThePeriodOnly()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Disp);
        app.Vm.PeriodAllCommand.Execute(null);
        var inPeriod = app.Vm.Tickets.Where(t => t.Date >= app.Vm.ReportFrom && t.Date <= app.Vm.ReportTo).ToList();

        Assert.Equal(inPeriod.Count.ToString(), app.Vm.ReportMetrics.Single(m => m.Name == "Принято заявок").Value);
        Assert.Equal(inPeriod.Count(t => t.Status == TicketStatus.Done).ToString(),
            app.Vm.ReportMetrics.Single(m => m.Name == "Выполнено заявок").Value);
        Assert.True(app.Vm.Tickets.Count > inPeriod.Count, "в учебной базе есть заявки вне периода отчёта");
    }

    [AvaloniaFact]
    public void AcceptTicket_AppearsInJournalAndOnlyAuthorEdits()
    {
        using var app = new DesktopApp("orlova");
        app.Go(AppModule.Disp);
        app.Vm.NewTicketFault = "Нет изображения на 1-м канале";

        app.Vm.AcceptTicketCommand.Execute(null);
        app.Pump();

        var ticket = app.Vm.SelectedTicket!;
        Assert.Equal("Нет изображения на 1-м канале", ticket.Fault);
        Assert.Contains(app.Vm.Tickets, t => t.Id == ticket.Id);
        Assert.True(app.Vm.CanEditSelectedTicket);

        app.Vm.SelectedTicket = app.Vm.Tickets.First(t => t.AcceptedByUserId != ticket.AcceptedByUserId);
        Assert.False(app.Vm.CanEditSelectedTicket);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Print_ReceiptAndAccountOpenAsPdf(bool receipt)
    {
        using var app = new DesktopApp("orlov");
        string? opened = null;
        app.Vm.OpenFile = path => opened = path;

        (receipt ? app.Vm.PrintReceiptCommand : app.Vm.PrintAccountCommand).Execute(null);

        Assert.NotNull(opened);
        Assert.StartsWith("%PDF", File.ReadAllText(opened)[..4]);
        Assert.StartsWith("Открыт PDF", app.Vm.Status);
    }

    [AvaloniaFact]
    public void Ppr_ClickOnMonthMarksItAndCountsPercent()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Ppr);
        var row = app.Vm.PprRows.First(r => r.Cells[7].Status == PprStatus.Empty);
        var houseId = row.House.Id;

        app.Vm.MarkPprCommand.Execute(row.Cells[7]);
        app.Pump();
        app.Vm.MarkPprCommand.Execute(app.Vm.PprRows.Single(r => r.House.Id == houseId).Cells[7]);
        app.Pump();

        var marked = app.Vm.PprRows.Single(r => r.House.Id == houseId);
        Assert.Equal(PprStatus.Done, marked.Cells[7].Status);
        Assert.Equal(DemoSeeder.WorkDate, marked.House.LastPpr);
        Assert.StartsWith($"Выполнено на {DemoSeeder.WorkDate:dd.MM.yyyy}:", app.Vm.PprSummary);
        Assert.Equal(app.Vm.PprRows.Count(r => r.Cells[7].Status != PprStatus.Empty), app.Vm.PprTotals[0].Values[7]);
    }

    [AvaloniaFact]
    public void Ppr_OnlyOverdueShowsHousesWithMissedPlan()
    {
        using var app = new DesktopApp("engineer");
        app.Go(AppModule.Ppr);

        app.Vm.PprOnlyOverdue = true;

        Assert.NotEmpty(app.Vm.PprRows);
        Assert.All(app.Vm.PprRows, r => Assert.True(r.HasOverdue));
    }

    [AvaloniaFact]
    public void Ppr_DispatcherSeesPlanButCannotMark()
    {
        using var app = new DesktopApp("sokolova");
        app.Go(AppModule.Ppr);
        var cell = app.Vm.PprRows.First().Cells[11];

        app.Vm.MarkPprCommand.Execute(cell);

        Assert.False(app.Vm.CanEditPpr);
        Assert.Equal(cell.Status, app.Vm.PprRows.First().Cells[11].Status);
    }

    [AvaloniaFact]
    public void LegalCalculator_ShowsDaysLeft()
    {
        using var app = new DesktopApp("lawyer");
        app.Go(AppModule.Legal);

        app.Vm.LegalStart = new DateTime(2026, 8, 3);

        Assert.Equal("осталось 2 дн. (до 02.09.2026)", app.Vm.LegalRemaining);
    }

    [AvaloniaFact]
    public void Report_ExportsCheckedMetricsByGroupToExcel()
    {
        using var app = new DesktopApp("lawyer");
        string? opened = null;
        app.Vm.OpenFile = path => opened = path;
        app.Vm.ReportMetrics.Single(m => m.Name == "Домов на обслуживании").On = false;

        app.Vm.ExportReportCommand.Execute(null);

        using var book = new ClosedXML.Excel.XLWorkbook(opened!);
        var sheet = book.Worksheet(1);
        Assert.Equal("Обслуживающая организация", sheet.Cell(3, 1).GetString());
        Assert.DoesNotContain(sheet.Row(3).CellsUsed(), c => c.GetString() == "Домов на обслуживании");
        var total = sheet.Column(1).CellsUsed().Single(c => c.GetString() == "Итого").Address.RowNumber;
        Assert.Equal(app.Vm.ReportMetrics.Single(m => m.Name == "Принято заявок").Value, sheet.Cell(total, 2).GetString());
    }

    [AvaloniaFact]
    public void Debtors_ExportToExcel()
    {
        using var app = new DesktopApp("lawyer");
        string? opened = null;
        app.Vm.OpenFile = path => opened = path;

        app.Vm.ExportDebtorsCommand.Execute(null);

        using var book = new ClosedXML.Excel.XLWorkbook(opened!);
        Assert.Equal(app.Vm.Debtors.Count, book.Worksheet(1).RowsUsed().Count() - 2);
    }
}
