using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avers.Desktop.Views;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class ContractScanScreenTests
{
    [AvaloniaFact]
    public async Task UploadAndOpenPaperContract_ThenChangingApartmentClearsOldFiles()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);
        var contracts = app.Vm.ContractsScreen;
        var number = app.Vm.SelectedSubscriber!.ContractNumber;
        var content = "%PDF-1.4\n%%EOF"u8.ToArray();
        var source = Path.Combine(Path.GetTempPath(), $"avers-contract-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(source, content);
        string? opened = null;
        app.Vm.OpenFile = path => opened = path;
        try
        {
            contracts.PickContractDocumentFile = () => Task.FromResult<string?>(source);
            await contracts.AddContractDocumentCommand.ExecuteAsync(null);
            var document = Assert.Single(contracts.ContractDocuments);
            Assert.Contains(number, document.Name);
            Assert.Null(contracts.SelectedContractDocument);
            contracts.SelectedContractDocument = document;
            await contracts.OpenContractDocumentCommand.ExecuteAsync(null);
            Assert.NotNull(opened);
            Assert.Equal(content, await File.ReadAllBytesAsync(opened));

            contracts.ContractQuery = "ТО-2011/180090";
            await contracts.FindContractCommand.ExecuteAsync(null);
            Assert.Empty(contracts.ContractDocuments);
            Assert.Null(contracts.SelectedContractDocument);
        }
        finally
        {
            File.Delete(source);
            if (opened is not null) File.Delete(opened);
        }
    }

    [AvaloniaFact]
    public void Contract_ShowsPaperScanActions_WithNoImplicitSelectedFile()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Contract);
        var view = Assert.Single(app.Window.GetVisualDescendants().OfType<ContractView>());
        var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
        Assert.Contains(buttons, b => Equals(b.Content, "Добавить скан"));
        var open = Assert.Single(buttons, b => Equals(b.Content, "Открыть скан"));
        Assert.NotNull(open.Command);
        Assert.False(open.Command.CanExecute(open.CommandParameter));
        Assert.False(open.IsEffectivelyEnabled);
    }
}
