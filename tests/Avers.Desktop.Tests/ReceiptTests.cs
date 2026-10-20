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
        app.Vm.ReceiptAccount = "180090";
        await app.Vm.FindReceiptsCommand.ExecuteAsync(null);
        Assert.Equal("180090", app.Vm.SelectedSubscriber?.Account);
        Assert.Empty(app.Vm.Receipts);

        app.Vm.ReceiptAccount = "1932068";
        await app.Vm.FindReceiptsCommand.ExecuteAsync(null);
        Assert.Equal(2, app.Vm.Receipts.Count);
        Assert.Equal(["Все периоды", "За июль 2026", "За июнь 2026"], app.Vm.ReceiptPeriods.Select(p => p.Title));

        app.Vm.SelectedReceiptPeriod = app.Vm.ReceiptPeriods[2];

        Assert.Equal("КВ-33004", Assert.Single(app.Vm.Receipts).NKvit);
    }
}
