using System.Diagnostics;
using Avers.Desktop.Printing;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>Печать квитанции и лицевого счёта выбранной квартиры: PDF во временной папке, открывается для печати.</summary>
public sealed partial class MainViewModel
{
    [ObservableProperty] private bool _printWithPayDate = true;

    /// <summary>Чем открыть готовый PDF; в тестах подменяется.</summary>
    public Action<string> OpenFile { get; set; } = path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public string ReceiptMonthsText => SelectedSubscriber is { } s ? $"{PdfForms.DueUnpaid(s, _today).Count} мес. без оплаты" : "";
    public string ReceiptSumText => SelectedSubscriber is { } s ? Money(PdfForms.DueUnpaid(s, _today).Sum(p => p.Tariff)) : "";

    [RelayCommand]
    private void PrintReceipt() =>
        Print("квитанция", "квитанция", (s, path) => PdfForms.Receipt(s, _today, path));

    [RelayCommand]
    private void PrintAccount() =>
        Print("лицевой-счёт", "лицевой счёт", (s, path) => PdfForms.Account(s, _today.Year, PrintWithPayDate, _today, path));

    private void Print(string file, string title, Action<Subscriber, string> write)
    {
        if (SelectedSubscriber is not { } s) return;
        var path = PrintPath(file, s);
        try
        {
            write(s, path);
            OpenFile(path);
            Status = $"Открыт PDF: {title} {s.FullName}. Печать — из программы просмотра.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Status = $"Не удалось открыть PDF: {e.Message}";
        }
    }

    private static string PrintPath(string kind, Subscriber s)
    {
        var dir = Path.Combine(Path.GetTempPath(), "Avers");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{kind}-{s.Account}.pdf");
    }
}
