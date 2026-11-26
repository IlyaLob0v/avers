using System.IO.Compression;
using Avalonia.Headless.XUnit;
using Avers.Desktop.ViewModels;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class LegalPackageTests
{
    private static readonly byte[] Pdf = "%PDF-1.4\n%%EOF"u8.ToArray();

    [AvaloniaFact]
    public async Task LegalEntity_DoesNotGetPersonCourtApplication()
    {
        using var app = new DesktopApp();
        var account = await SelectDebtorCard(app);
        app.Vm.EditIsLegalEntity = true;
        await app.Vm.SaveCardCommand.ExecuteAsync(null);
        app.Go(AppModule.Claim);
        app.Vm.LegalScreen.SelectedDebtor = app.Vm.LegalScreen.Debtors.Single(d => d.Account == account);
        var opened = new List<string>();
        app.Vm.OpenFile = opened.Add;

        await app.Vm.LegalScreen.PrintCourtOrderCommand.ExecuteAsync(null);

        Assert.Empty(opened);
        Assert.Contains("юрлиц", app.Vm.Status);
    }

    [AvaloniaFact]
    public async Task Package_IncludesContractScan_ButNotCourtFiles()
    {
        using var app = new DesktopApp("lawyer");
        app.Go(AppModule.Claim);
        var legal = app.Vm.LegalScreen;
        legal.SelectedDebtor = legal.Debtors.First();
        await AddScan(legal, "Договор");
        await AddScan(legal, "Решение суда");
        string? opened = null;
        app.Vm.OpenFile = path => opened = path;

        await legal.ExportCourtPackageCommand.ExecuteAsync(null);

        Assert.NotNull(opened);
        Assert.EndsWith(".zip", opened);
        using (var archive = ZipFile.OpenRead(opened))
        {
            Assert.Contains(archive.Entries, e => e.Name == "расчёт.pdf");
            var scan = Assert.Single(archive.Entries, e => e.FullName.StartsWith("сканы/"));
            using var content = new MemoryStream();
            using (var stream = scan.Open()) await stream.CopyToAsync(content);
            Assert.Equal(Pdf, content.ToArray());
            using var reader = new StreamReader(archive.GetEntry("Состав.txt")!.Open());
            var composition = await reader.ReadToEndAsync();
            Assert.Contains("Физлицо", composition);
            Assert.Contains("Договор", composition);
        }
        File.Delete(opened);
    }

    [AvaloniaFact]
    public async Task LegalEntity_GetsCalculationPackageWithRequisites_AndCannotPrintPersonForms()
    {
        using var app = new DesktopApp();
        var account = await SelectDebtorCard(app);
        app.Vm.EditIsLegalEntity = true;
        app.Vm.EditInn = "5503254160";
        app.Vm.EditKpp = "550301001";
        app.Vm.EditOgrn = "1145543048001";
        app.Vm.EditLegalAddress = "г. Омск, юридический адрес";
        app.Vm.EditDirector = "Директор организации";
        await app.Vm.SaveCardCommand.ExecuteAsync(null);
        app.Go(AppModule.Claim);
        var legal = app.Vm.LegalScreen;
        legal.SelectedDebtor = legal.Debtors.Single(d => d.Account == account);
        var opened = new List<string>();
        app.Vm.OpenFile = opened.Add;

        await legal.ExportCourtPackageCommand.ExecuteAsync(null);

        var package = Assert.Single(opened);
        Assert.EndsWith(".zip", package);
        using (var archive = ZipFile.OpenRead(package))
        {
            Assert.Contains(archive.Entries, e => e.Name == "расчёт.pdf");
            using var reader = new StreamReader(archive.GetEntry("Состав.txt")!.Open());
            var composition = await reader.ReadToEndAsync();
            Assert.Contains("Юрлицо", composition);
            Assert.Contains("5503254160", composition);
            Assert.Contains("Юридический адрес: г. Омск, юридический адрес", composition);
            Assert.Contains("Адрес обслуживания:", composition);
            Assert.Contains("шаблон", composition);
            Assert.DoesNotContain(archive.Entries, e => e.Name.EndsWith(".docx"));
        }
        File.Delete(package);
        opened.Clear();

        await legal.PrintCourtOrderCommand.ExecuteAsync(null);
        Assert.Empty(opened);
        Assert.Contains("юрлиц", app.Vm.Status);
        await legal.PrintEnforcementCommand.ExecuteAsync(null);
        Assert.Empty(opened);
        Assert.Contains("юрлиц", app.Vm.Status);
        legal.PrintClaimCommand.Execute(null);
        Assert.Empty(opened);
        Assert.Contains("юрлиц", app.Vm.Status);
        await legal.PrintPenaltyCommand.ExecuteAsync(null);
        Assert.Empty(opened);
        Assert.Contains("юрлиц", app.Vm.Status);
        app.Go(AppModule.Contract);
        app.Vm.PrintContractCommand.Execute(null);
        Assert.Empty(opened);
        Assert.Contains("юрлиц", app.Vm.Status);
    }

    private static async Task AddScan(LegalViewModel legal, string kind)
    {
        var scan = Path.Combine(Path.GetTempPath(), $"avers-package-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(scan, Pdf);
        try
        {
            legal.PickDocumentFile = () => Task.FromResult<string?>(scan);
            legal.DocumentKind = kind;
            await legal.AddDocumentCommand.ExecuteAsync(null);
        }
        finally { File.Delete(scan); }
    }

    private static async Task<string> SelectDebtorCard(DesktopApp app)
    {
        app.Go(AppModule.Legal);
        var account = app.Vm.LegalScreen.Debtors.First().Account;
        app.Go(AppModule.Abon);
        app.Vm.SearchMode = SearchMode.Account;
        app.Vm.FindQuery = account;
        await app.Vm.FindCommand.ExecuteAsync(null);
        Assert.Equal(account, app.Vm.SelectedSubscriber?.Account);
        return account;
    }
}
