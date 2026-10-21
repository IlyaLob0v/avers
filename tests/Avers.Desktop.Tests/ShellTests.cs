using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Data;
using Avers.Desktop.Controls;
using Avers.Desktop.ViewModels;
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
    public async Task ChangeTariff_ForThisHouse_ChangesUnpaidMonths()
    {
        using var app = new DesktopApp("admin");
        app.Go(AppModule.Abon);
        var s = app.Vm.SelectedSubscriber!;
        app.Go(AppModule.Subscriber);

        app.Vm.TariffFrom = "01." + app.Vm.CardPaymentYear;
        app.Vm.NewTariffValue = "120";
        app.Vm.TariffScope = MainViewModel.TariffScopes[1];
        await app.Vm.ChangeTariffCommand.ExecuteAsync(null);

        Assert.Contains("изменено неоплаченных месяцев", app.Vm.Status);
        Assert.All(app.Vm.SelectedSubscriber!.Payments.Where(p => p.Amount is null), p => Assert.Equal(120m, p.Tariff));
        Assert.Equal(s.Id, app.Vm.SelectedSubscriber.Id);
    }

    [AvaloniaFact]
    public async Task CancelPayment_SelectedPaidMonthBecomesUnpaid()
    {
        using var app = new DesktopApp("orlov");
        app.Go(AppModule.Abon);
        await app.Vm.RecordPaymentCommand.ExecuteAsync(null);
        var id = app.Vm.SelectedSubscriber!.Id;
        app.Go(AppModule.Subscriber);

        app.Vm.SelectedCardPayment = app.Vm.CardPayments.Last(r => r.Payment.Amount is not null);
        var month = app.Vm.SelectedCardPayment.Payment.Month;
        await app.Vm.CancelPaymentCommand.ExecuteAsync(null);

        Assert.EndsWith("отменена.", app.Vm.Status);
        Assert.Equal(id, app.Vm.SelectedSubscriber!.Id);
        Assert.Null(app.Vm.SelectedSubscriber.Payments.Single(p => p.Year == app.Vm.CardPaymentYear && p.Month == month).Amount);
    }

    [AvaloniaFact]
    public void OpenYear_ReportsOpenedSubscribers()
    {
        using var app = new DesktopApp("admin");

        app.Vm.OpenYearCommand.Execute(null);
        app.Pump();

        Assert.StartsWith("Начисления открыты:", app.Vm.Status);
        Assert.DoesNotContain("— 0.", app.Vm.Status);
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
    public void Claim_FollowsLawyersSampleForSelectedDebtor()
    {
        using var app = new DesktopApp("lawyer");

        foreach (var debtor in app.Vm.Debtors.ToList())
        {
            app.Vm.SelectedDebtor = debtor;
            Assert.Contains(MoneyWords.Rubles(debtor.Debt), app.Vm.ClaimText);
            Assert.Contains($"г. Омск, {debtor.Address}", app.Vm.ClaimText);
            Assert.Contains("10 (десяти) календарных дней", app.Vm.ClaimText);
        }

        app.Vm.ClaimVariant = app.Vm.ClaimVariants[1];
        Assert.StartsWith("Досудебное уведомление о погашении задолженности", app.Vm.ClaimText);
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
    public void Print_WorkOrderOfSelectedTicketOpensAsPdf()
    {
        using var app = new DesktopApp("orlov");
        app.Go(AppModule.Disp);
        string? opened = null;
        app.Vm.OpenFile = path => opened = path;

        app.Vm.PrintWorkOrderCommand.Execute(null);

        Assert.NotNull(opened);
        Assert.StartsWith("%PDF", File.ReadAllText(opened)[..4]);
        Assert.Contains($"наряд-заказ № {app.Vm.SelectedTicket!.Number}", app.Vm.Status);
    }

    [AvaloniaFact]
    public async Task CourtCase_LawyerAddsCase_ShownInDebtors()
    {
        using var app = new DesktopApp("lawyer");
        app.Go(AppModule.Claim);
        app.Pump();
        var debtor = app.Vm.Debtors.First(d => d.CourtCase is null);
        app.Vm.SelectedDebtor = debtor;

        app.Vm.NewCourtCaseCommand.Execute(null);
        app.Vm.CaseOrder = "2-9/2026";
        app.Vm.CaseCourt = "Мировой судья с/у № 47";
        await app.Vm.SaveCourtCaseCommand.ExecuteAsync(null);

        Assert.True(app.Vm.CanEditCourtCases);
        Assert.Equal(" · 2-9/2026", app.Vm.Debtors.Single(d => d.SubscriberId == debtor.SubscriberId).Case);
        Assert.Equal("2-9/2026", app.Vm.CaseOrder);
    }

    [AvaloniaFact]
    public async Task Claim_LawyerAddsOpensAndDeletesScan()
    {
        using var app = new DesktopApp("lawyer");
        var file = Path.Combine(Path.GetTempPath(), $"Договор-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(file, "%PDF-1.4\n%%EOF"u8.ToArray());
        try
        {
            app.Go(AppModule.Claim);
            app.Pump();
            var opened = new List<string>();
            app.Vm.OpenFile = opened.Add;

            app.Vm.PickDocumentFile = () => Task.FromResult<string?>(file);
            await app.Vm.AddDocumentCommand.ExecuteAsync(null);
            app.Vm.SelectedDocument = app.Vm.Documents.Single();
            await app.Vm.OpenDocumentCommand.ExecuteAsync(null);

            Assert.Equal(("Договор", Path.GetFileNameWithoutExtension(file)), (app.Vm.SelectedDocument!.Kind, app.Vm.SelectedDocument.Name));
            Assert.EndsWith(".pdf", opened.Single());
            Assert.StartsWith("%PDF", File.ReadAllText(opened.Single()));

            await app.Vm.DeleteDocumentCommand.ExecuteAsync(null);
            await app.Vm.DeleteDocumentCommand.ExecuteAsync(null);
            Assert.Empty(app.Vm.Documents);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [AvaloniaFact]
    public async Task Claim_OpensCourtOrderApplicationInWord()
    {
        using var app = new DesktopApp("lawyer");
        app.Go(AppModule.Claim);
        var opened = new List<string>();
        app.Vm.OpenFile = opened.Add;
        var debtor = app.Vm.SelectedDebtor!;

        await app.Vm.PrintCourtOrderCommand.ExecuteAsync(null);

        using var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(opened.Single(), false);
        var text = word.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Заявление о выдаче судебного приказа", text);
        Assert.Contains(MoneyWords.Rubles(debtor.Debt), text);
        Assert.Contains("Коньшин С.А.", text);
    }

    [AvaloniaFact]
    public async Task Claim_OpensEnforcementApplicationWithCourtCase()
    {
        using var app = new DesktopApp("lawyer");
        app.Go(AppModule.Claim);
        app.Pump();
        var opened = new List<string>();
        app.Vm.OpenFile = opened.Add;
        app.Vm.SelectedDebtor = app.Vm.Debtors.First(d => d.CourtCase is { OrderNumber: not "" });
        var order = app.Vm.SelectedDebtor.CourtCase!;

        await app.Vm.PrintEnforcementCommand.ExecuteAsync(null);

        using var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(opened.Single(), false);
        var text = word.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Заявление о возбуждении исполнительного производства", text);
        Assert.Contains($"судебного приказа № {order.OrderNumber} от {order.OrderDate:dd.MM.yyyy}", text);
        Assert.Contains(MoneyWords.Rubles(order.Amount), text);
    }

    [AvaloniaFact]
    public async Task Claim_PrintsPenaltyOnChosenDate()
    {
        using var app = new DesktopApp("lawyer");
        app.Go(AppModule.Claim);
        var opened = new List<string>();
        app.Vm.OpenFile = opened.Add;

        app.Vm.PenaltyDate = new DateTime(2026, 11, 1);
        await app.Vm.PrintPenaltyCommand.ExecuteAsync(null);

        Assert.StartsWith("%PDF", File.ReadAllText(opened.Single())[..4]);
        Assert.Contains("расчёт неустойки", app.Vm.Status);
    }

    [AvaloniaFact]
    public async Task Claim_OpensNoticeInWordAndCourtPackageAsPdf()
    {
        using var app = new DesktopApp("lawyer");
        app.Go(AppModule.Claim);
        var opened = new List<string>();
        app.Vm.OpenFile = opened.Add;

        app.Vm.PrintClaimCommand.Execute(null);
        await app.Vm.ExportCourtPackageCommand.ExecuteAsync(null);

        Assert.Equal(2, opened.Count);
        using (var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(opened[0], false))
            Assert.Contains("10 (десяти) календарных дней", word.MainDocumentPart!.Document!.Body!.InnerText);
        Assert.StartsWith("%PDF", File.ReadAllText(opened[1])[..4]);
        var pages = System.Text.RegularExpressions.Regex.Count(File.ReadAllText(opened[1]), @"/Type /Page\b");
        Assert.True(pages >= 2, $"В пакете {pages} стр.: справка и хотя бы один год оплат.");
        Assert.Contains("пакет для суда", app.Vm.Status);
    }

    [AvaloniaFact]
    public void Back_ReturnsToPreviousScreens()
    {
        using var app = new DesktopApp("admin");
        var start = app.Vm.Module;
        app.Go(AppModule.Disp);
        app.Go(AppModule.Anten);

        app.Vm.BackCommand.Execute(null);
        Assert.Equal(AppModule.Disp, app.Vm.Module);
        app.Vm.BackCommand.Execute(null);
        Assert.Equal(start, app.Vm.Module);
        Assert.False(app.Vm.BackCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void About_ShowsVersionAndDatabase()
    {
        using var app = new DesktopApp();
        var about = new Views.AboutWindow { DataContext = app.Vm };

        about.Show(app.Window);
        app.Pump();

        Assert.StartsWith("Версия ", about.FindControl<TextBlock>("VersionText")!.Text);
        Assert.NotNull(about.Icon);
        Assert.NotNull(about.CaptureRenderedFrame());
        about.Close();
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
