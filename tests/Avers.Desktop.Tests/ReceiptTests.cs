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
}
