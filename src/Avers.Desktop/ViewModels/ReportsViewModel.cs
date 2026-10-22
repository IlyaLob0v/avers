using System.Collections.ObjectModel;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.Reports;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Отчёты в Excel: итоговый по заявкам за период с разбивкой по обслуживающим организациям или зонам
/// (метрики включаются и выключаются) и итоги месяца для 1С.
/// </summary>
public sealed partial class ReportsViewModel(MainViewModel shell) : ObservableObject
{
    public ObservableCollection<ReportMetric> ReportMetrics { get; } = [];

    [ObservableProperty] private DateOnly _reportFrom = new(shell.Today.Year, 1, 1);
    [ObservableProperty] private DateOnly _reportTo = shell.Today;
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
    internal void RefreshReport()
    {
        var off = ReportMetrics.Where(m => !m.On).Select(m => m.Name).ToHashSet();
        ReportMetrics.Clear();
        foreach (var (name, value) in ReportDefinitions())
        {
            var total = value(shell.Houses);
            ReportMetrics.Add(new ReportMetric(name, total is decimal d ? MainViewModel.Money(d) : total.ToString()!, !off.Contains(name)));
        }
    }

    private IEnumerable<(string Name, Func<IReadOnlyCollection<House>, object> Value)> ReportDefinitions()
    {
        IEnumerable<WorkTicket> Tickets(IReadOnlyCollection<House> houses) =>
            shell.AllTickets.Where(t => t.Date >= ReportFrom && t.Date <= ReportTo && houses.Any(h => h.Id == t.HouseId));

        yield return ("Домов на обслуживании", hs => hs.Count(h => h.Serviced));
        yield return ("Принято заявок", hs => Tickets(hs).Count());
        yield return ("Выполнено заявок", hs => Tickets(hs).Count(t => t.Status == TicketStatus.Done));
        yield return ("Неустранённые неисправности СКПТ", hs => hs.Count(h => h.OpenFault != null));
        yield return ("Сумма дебиторской задолженности", hs =>
            hs.Sum(h => shell.DebtByHouse.GetValueOrDefault(h.Id)));
    }

    [RelayCommand]
    private void ExportReport()
    {
        var metrics = ReportDefinitions().Where(d => ReportMetrics.Any(m => m.Name == d.Name && m.On)).ToList();
        if (metrics.Count == 0)
        {
            shell.Status = "Отметьте хотя бы одну метрику.";
            return;
        }
        var groups = shell.Houses
            .GroupBy(h => ReportByZone ? Blank(h.Zone, "без зоны") : Blank(h.Organization?.Name, "без организации"))
            .OrderBy(g => g.Key)
            .Select(g => (Name: g.Key, Houses: (IReadOnlyCollection<House>)g.ToList()))
            .Append((Name: "Итого", Houses: shell.Houses))
            .Select(g => metrics.Select(m => m.Value(g.Houses)).Prepend(g.Name).ToArray());

        shell.Export("отчёт-по-заявкам", "отчёт по заявкам", path => XlsxTable.Write(path,
            $"Итоговый отчёт по заявкам с {ReportFrom:dd.MM.yyyy} по {ReportTo:dd.MM.yyyy}",
            metrics.Select(m => m.Name).Prepend(ReportByZone ? "Зона обслуживания" : "Обслуживающая организация").ToList(),
            groups));
    }

    /// <summary>Итоги месяца начальной даты для 1С: отгрузка по тарифам и поступления по источникам.</summary>
    [RelayCommand]
    private async Task ExportForOneC()
    {
        AccountingReport report;
        try
        {
            report = await shell.Store.LoadAccountingAsync(ReportFrom.Year, ReportFrom.Month);
        }
        catch (StoreException e)
        {
            shell.Status = e.Message;
            return;
        }
        var (shipment, receipts) = (report.Shipment, report.Receipts);
        var rows = shipment.Select(l => new object?[] { "Отгрузка", "Абонентская плата", l.Count, l.Tariff, l.Sum })
            .Append(["Отгрузка", "Итого", shipment.Sum(l => l.Count), null, shipment.Sum(l => l.Sum)])
            .Concat(receipts.Select(l => new object?[] { "Поступления", l.Source, l.Count, null, l.Sum }))
            .Append(["Поступления", "Итого", receipts.Sum(l => l.Count), null, receipts.Sum(l => l.Sum)]);

        shell.Export("для-1с", "итоги месяца для 1С", path => XlsxTable.Write(path,
            $"Для 1С: отгрузка и поступления за {ReportFrom:MM.yyyy}",
            ["Раздел", "Услуга / источник", "Количество", "Цена", "Сумма"],
            rows));
    }

    private static string Blank(string? value, string empty) => string.IsNullOrWhiteSpace(value) ? empty : value;
}
