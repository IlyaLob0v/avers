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

        app.Vm.ContractsScreen.ContractQuery = "ТО-2011/180090";
        await app.Vm.ContractsScreen.FindContractCommand.ExecuteAsync(null);

        Assert.Equal("ул. Братская, д. 18", app.Vm.SelectedHouse?.AddressLine);
        Assert.Equal("180090", app.Vm.ContractsScreen.SelectedContractRow?.Account);
    }

    [AvaloniaFact]
    public async Task PrintContract_FillsWhatAversKnows_LeavesPassportBlank()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);
        var opened = new List<string>();
        app.Vm.OpenFile = opened.Add;
        app.Vm.ContractsScreen.ContractQuery = "ТО-2011/180090";
        await app.Vm.ContractsScreen.FindContractCommand.ExecuteAsync(null);

        app.Vm.PrintContractCommand.Execute(null);

        using var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(opened.Single(), false);
        var text = word.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("ДОГОВОР № ТО-2011/180090", text);
        Assert.Contains("«01» сентября 2011 г.", text);
        Assert.Contains("гр-н (-ка) Савельев Игорь Дмитриевич", text);
        Assert.Contains("г. Омск, ул. Братская, д. 18, корп. _____ кв. 90", text);
        Assert.Contains(MoneyWords.Rubles(95), text);
        Assert.Contains("СОГЛАСИЕ НА ОБРАБОТКУ ПЕРСОНАЛЬНЫХ ДАННЫХ", text);
        Assert.Contains("ИНН ______", text);
        Assert.Contains("СНИЛС ______", text);
    }

    [AvaloniaFact]
    public async Task FindBySurname_WithoutDigits()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);

        app.Vm.ContractsScreen.ContractQuery = "Чернова";
        await app.Vm.ContractsScreen.FindContractCommand.ExecuteAsync(null);

        Assert.Equal("ТО-2001/180015", app.Vm.ContractsScreen.SelectedContractRow?.Number);
    }

    [AvaloniaFact]
    public async Task NewApartment_AppearsInRegistryWithAccruals()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);
        var house = app.Vm.SelectedHouse!;

        app.Vm.ContractsScreen.StartNewApartmentCommand.Execute(null);
        app.Vm.ContractsScreen.NewApartment = "99";
        app.Vm.ContractsScreen.NewAccount = "990099";
        app.Vm.ContractsScreen.NewLastName = "Смирнов";
        app.Vm.ContractsScreen.NewContractNumber = "ТО-2026/990099";
        app.Vm.ContractsScreen.NewTariff = "95,50";
        await app.Vm.ContractsScreen.AddApartmentCommand.ExecuteAsync(null);

        Assert.False(app.Vm.ContractsScreen.IsNewApartmentOpen);
        Assert.Equal(house.Id, app.Vm.SelectedHouse?.Id);
        Assert.Contains(app.Vm.ContractsScreen.ContractRows, r => r.Account == "990099" && r.Number == "ТО-2026/990099");
        Assert.Equal("990099", app.Vm.SelectedSubscriber?.Account);
        Assert.All(app.Vm.SelectedSubscriber!.Payments, p => Assert.Equal(95.50m, p.Tariff));
        Assert.NotEmpty(app.Vm.SelectedSubscriber.Payments);
    }

    [AvaloniaFact]
    public async Task NewApartment_DuplicateAccount_SaysSo()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);

        app.Vm.ContractsScreen.StartNewApartmentCommand.Execute(null);
        app.Vm.ContractsScreen.NewApartment = "99";
        app.Vm.ContractsScreen.NewAccount = "180090";
        app.Vm.ContractsScreen.NewLastName = "Смирнов";
        app.Vm.ContractsScreen.NewTariff = "95";
        await app.Vm.ContractsScreen.AddApartmentCommand.ExecuteAsync(null);

        Assert.Equal("Лицевой счёт 180090 уже есть.", app.Vm.Status);
        Assert.True(app.Vm.ContractsScreen.IsNewApartmentOpen);
    }

    [AvaloniaFact]
    public async Task Terminate_ThenNewContract()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);
        app.Vm.ContractsScreen.ContractQuery = "ТО-2011/180090";
        await app.Vm.ContractsScreen.FindContractCommand.ExecuteAsync(null);

        await app.Vm.TerminateContractCommand.ExecuteAsync(null);
        Assert.Equal(DemoSeeder.WorkDate, app.Vm.SelectedSubscriber?.ContractTerminated);
        Assert.DoesNotContain(app.Vm.ContractsScreen.ContractRows, r => r.Account == "180090");

        app.Vm.ContractsScreen.OnlyActiveContracts = false;
        app.Vm.ContractsScreen.SelectedContractRow = app.Vm.ContractsScreen.ContractRows.Single(r => r.Account == "180090");
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

        app.Vm.ContractsScreen.ExportContractsCommand.Execute(null);

        Assert.NotNull(opened);
        Assert.True(new FileInfo(opened).Length > 0);
        File.Delete(opened);
    }
}
