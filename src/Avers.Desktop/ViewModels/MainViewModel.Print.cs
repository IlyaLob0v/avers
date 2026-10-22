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
        OpenPdf(PrintPath(file, s.Account), $"{title} {s.FullName}", path => write(s, path));
    }

    internal void OpenPdf(string path, string title, Action<string> write)
    {
        try
        {
            write(path);
            OpenFile(path);
            Status = $"Открыт PDF: {title}. Печать — из программы просмотра.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Status = $"Не удалось открыть PDF: {e.Message}";
        }
    }

    /// <summary>Пишет документ Word и открывает его.</summary>
    internal void OpenWord(string path, string title, Action<string> write)
    {
        try
        {
            write(path);
            OpenFile(path);
            Status = $"Открыто в Word: {title}.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Status = $"Не удалось открыть {title}: {e.Message}";
        }
    }

    internal static string PrintPath(string kind, string key, string extension = ".pdf")
    {
        var dir = Path.Combine(Path.GetTempPath(), "Avers");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{kind}-{key}{extension}");
    }

    /// <summary>Записывает файл Excel во временную папку и открывает его.</summary>
    internal void Export(string file, string title, Action<string> write)
    {
        var dir = Path.Combine(Path.GetTempPath(), "Avers");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{file}-{_today:yyyy-MM-dd}.xlsx");
        try
        {
            write(path);
            OpenFile(path);
            Status = $"Открыт Excel: {title}.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Status = $"Не удалось открыть отчёт: {e.Message}";
        }
    }
}
