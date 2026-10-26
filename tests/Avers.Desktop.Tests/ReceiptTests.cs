using Avalonia.Headless.XUnit;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class ReceiptTests
{
    [AvaloniaFact]
    public async Task FindAnyAccount_ThenFilterByMonth()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.PrDubl);
        app.Vm.ReceiptsScreen.ReceiptAccount = "180090";
        await app.Vm.ReceiptsScreen.FindReceiptsCommand.ExecuteAsync(null);
        Assert.Equal("180090", app.Vm.SelectedSubscriber?.Account);
        Assert.Empty(app.Vm.ReceiptsScreen.Receipts);

        app.Vm.ReceiptsScreen.ReceiptAccount = "1932068";
        await app.Vm.ReceiptsScreen.FindReceiptsCommand.ExecuteAsync(null);
        Assert.Equal(2, app.Vm.ReceiptsScreen.Receipts.Count);
        Assert.Equal(["Все периоды", "За июль 2026", "За июнь 2026"], app.Vm.ReceiptsScreen.ReceiptPeriods.Select(p => p.Title));

        app.Vm.ReceiptsScreen.SelectedReceiptPeriod = app.Vm.ReceiptsScreen.ReceiptPeriods[2];

        Assert.Equal("КВ-33004", Assert.Single(app.Vm.ReceiptsScreen.Receipts).NKvit);
    }

    [AvaloniaFact]
    public async Task OekFile_OfAnotherMonth_IsNotLoaded()
    {
        using var app = new DesktopApp("orlov");
        app.Go(AppModule.PrDubl);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "nach082026_an.xlsx");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var book = new ClosedXML.Excel.XLWorkbook())
        {
            var sheet = book.AddWorksheet("Лист1");
            string[] headers = ["ACCOUNT,C,11", "LS,C,11", "ADDRESS,C,100", "NKWART,C,10", "NACH_AN,N,10,2", "SUMMA_AN,N,10,2", "DATE_PAY,D"];
            for (var c = 0; c < headers.Length; c++)
                sheet.Cell(1, c + 1).Value = headers[c];
            sheet.Cell(2, 1).Value = "96010170082";
            sheet.Cell(2, 2).Value = "1932068";
            sheet.Cell(2, 6).Value = 95;
            book.SaveAs(path);
        }
        app.Vm.ReceiptsScreen.OekMonth = "07.2026";

        await app.Vm.ReceiptsScreen.ImportOekFileAsync(path);
        Directory.Delete(Path.GetDirectoryName(path)!, true);

        Assert.Contains("08.2026", app.Vm.Status);
        Assert.Contains("07.2026", app.Vm.Status);
        Assert.DoesNotContain(app.Vm.SelectedSubscriber!.Payments, p => p.Source == "ОЭК" && p.Month == 7 && p.RecordedBy == "Файл ОЭК");
    }
}
