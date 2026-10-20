using Avalonia.Headless.XUnit;
using Avers.Data;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class ContractTests
{
    [AvaloniaFact]
    public async Task FindByNumber_OpensHouseOfContract()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);

        app.Vm.ContractQuery = "ТО-2011/180090";
        await app.Vm.FindContractCommand.ExecuteAsync(null);

        Assert.Equal("ул. Братская, д. 18", app.Vm.SelectedHouse?.AddressLine);
        Assert.Equal("180090", app.Vm.SelectedContractRow?.Account);
    }

    [AvaloniaFact]
    public async Task FindBySurname_WithoutDigits()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);

        app.Vm.ContractQuery = "Чернова";
        await app.Vm.FindContractCommand.ExecuteAsync(null);

        Assert.Equal("ТО-2001/180015", app.Vm.SelectedContractRow?.Number);
    }

    [AvaloniaFact]
    public async Task Terminate_ThenNewContract()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);
        app.Vm.ContractQuery = "ТО-2011/180090";
        await app.Vm.FindContractCommand.ExecuteAsync(null);

        await app.Vm.TerminateContractCommand.ExecuteAsync(null);
        Assert.Equal(DemoSeeder.WorkDate, app.Vm.SelectedSubscriber?.ContractTerminated);
        Assert.DoesNotContain(app.Vm.ContractRows, r => r.Account == "180090");

        app.Vm.OnlyActiveContracts = false;
        app.Vm.SelectedContractRow = app.Vm.ContractRows.Single(r => r.Account == "180090");
        app.Vm.NewContractCommand.Execute(null);
        app.Vm.EditContractNumber = "ТО-2026/180090";
        await app.Vm.SaveCardCommand.ExecuteAsync(null);

        var s = app.Vm.SelectedSubscriber!;
        Assert.Equal(("ТО-2026/180090", DemoSeeder.WorkDate, (DateOnly?)null), (s.ContractNumber, s.ContractDate, s.ContractTerminated));
    }

    [AvaloniaFact]
    public void Registry_ExportsHouseContracts()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);
        string? opened = null;
        app.Vm.OpenFile = path => opened = path;

        app.Vm.ExportContractsCommand.Execute(null);

        Assert.NotNull(opened);
        Assert.True(new FileInfo(opened).Length > 0);
        File.Delete(opened);
    }
}
