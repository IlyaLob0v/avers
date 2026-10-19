using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.Reports;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Отчёты в Excel: итоговый по заявкам за период с разбивкой по обслуживающим организациям или зонам
/// (метрики включаются и выключаются) и список должников для юристов.
/// </summary>
public sealed partial class MainViewModel
{
    [ObservableProperty] private bool _reportByZone;

    public DateTime? ReportFromDate
    {
        get => ReportFrom.ToDateTime(TimeOnly.MinValue);
        set { if (value is { } v) ReportFrom = DateOnly.FromDateTime(v); }
    }

    public DateTime? ReportToDate
    {
        get => ReportTo.ToDateTime(TimeOnly.MinValue);
        set { if (value is { } v) ReportTo = DateOnly.FromDateTime(v); }
    }

    partial void OnReportFromChanged(DateOnly value)
    {
        OnPropertyChanged(nameof(ReportFromDate));
        RefreshReport();
    }

    partial void OnReportToChanged(DateOnly value)
    {
        OnPropertyChanged(nameof(ReportToDate));
        RefreshReport();
    }

    /// <summary>Метрики отчёта и их значения по всей базе за период; отметки «в отчёт» сохраняются.</summary>
    private void RefreshReport()
    {
        var off = ReportMetrics.Where(m => !m.On).Select(m => m.Name).ToHashSet();
        ReportMetrics.Clear();
        foreach (var (name, value) in ReportDefinitions())
        {
            var total = value(_houses);
            ReportMetrics.Add(new ReportMetric(name, total is decimal d ? Money(d) : total.ToString()!, !off.Contains(name)));
        }
    }

    private IEnumerable<(string Name, Func<IReadOnlyCollection<House>, object> Value)> ReportDefinitions()
    {
        IEnumerable<WorkTicket> Tickets(IReadOnlyCollection<House> houses) =>
            _tickets.Where(t => t.Date >= ReportFrom && t.Date <= ReportTo && houses.Any(h => h.Id == t.HouseId));

        yield return ("Домов на обслуживании", hs => hs.Count(h => h.Serviced));
        yield return ("Принято заявок", hs => Tickets(hs).Count());
        yield return ("Выполнено заявок", hs => Tickets(hs).Count(t => t.Status == TicketStatus.Done));
        yield return ("Неустранённые неисправности СКПТ", hs => hs.Count(h => h.OpenFault != null));
        yield return ("Сумма дебиторской задолженности", hs =>
            hs.Sum(h => _debtByHouse.GetValueOrDefault(h.Id)));
    }

    [RelayCommand]
    private void ExportReport()
    {
        var metrics = ReportDefinitions().Where(d => ReportMetrics.Any(m => m.Name == d.Name && m.On)).ToList();
        if (metrics.Count == 0)
        {
            Status = "Отметьте хотя бы одну метрику.";
            return;
        }
        var groups = _houses
            .GroupBy(h => ReportByZone ? Blank(h.Zone, "без зоны") : Blank(h.Organization?.Name, "без организации"))
            .OrderBy(g => g.Key)
            .Select(g => (Name: g.Key, Houses: (IReadOnlyCollection<House>)g.ToList()))
            .Append((Name: "Итого", Houses: _houses))
            .Select(g => metrics.Select(m => m.Value(g.Houses)).Prepend(g.Name).ToArray());

        Export("отчёт-по-заявкам", "отчёт по заявкам", path => XlsxTable.Write(path,
            $"Итоговый отчёт по заявкам с {ReportFrom:dd.MM.yyyy} по {ReportTo:dd.MM.yyyy}",
            metrics.Select(m => m.Name).Prepend(ReportByZone ? "Зона обслуживания" : "Обслуживающая организация").ToList(),
            groups));
    }

    [RelayCommand]
    private void ExportDebtors() =>
        Export("должники", "отчёт по должникам", path => XlsxTable.Write(path,
            $"Должники на {_today:dd.MM.yyyy}",
            ["ФИО", "Адрес", "Лиц. счёт", "Долг, руб.", "Неоплачено мес.", "Последняя оплата", "Дело-Аверс"],
            Debtors.Select(d => new object?[] { d.FullName, d.Address, d.Account, d.Debt, d.Unpaid, d.LastPay, d.Case })));

    /// <summary>Итоги месяца начальной даты для 1С: отгрузка по тарифам и поступления по источникам.</summary>
    [RelayCommand]
    private async Task ExportForOneC()
    {
        AccountingReport report;
        try
        {
            report = await _store.LoadAccountingAsync(ReportFrom.Year, ReportFrom.Month);
        }
        catch (StoreException e)
        {
            Status = e.Message;
            return;
        }
        var (shipment, receipts) = (report.Shipment, report.Receipts);
        var rows = shipment.Select(l => new object?[] { "Отгрузка", "Абонентская плата", l.Count, l.Tariff, l.Sum })
            .Append(["Отгрузка", "Итого", shipment.Sum(l => l.Count), null, shipment.Sum(l => l.Sum)])
            .Concat(receipts.Select(l => new object?[] { "Поступления", l.Source, l.Count, null, l.Sum }))
            .Append(["Поступления", "Итого", receipts.Sum(l => l.Count), null, receipts.Sum(l => l.Sum)]);

        Export("для-1с", "итоги месяца для 1С", path => XlsxTable.Write(path,
            $"Для 1С: отгрузка и поступления за {ReportFrom:MM.yyyy}",
            ["Раздел", "Услуга / источник", "Количество", "Цена", "Сумма"],
            rows));
    }

    private void Export(string file, string title, Action<string> write)
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

    private static string Blank(string? value, string empty) => string.IsNullOrWhiteSpace(value) ? empty : value;
}
