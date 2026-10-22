using System.Collections.ObjectModel;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Графики ППР: план по домам на год, отметка о выполнении щелчком по месяцу, итоги по месяцам,
/// процент выполнения на рабочую дату, просроченные работы и перенос плана на следующий год.
/// </summary>
public sealed partial class PprViewModel(MainViewModel shell) : ObservableObject
{
    public const string PprAllHouses = "все дома";

    public ObservableCollection<PprRow> PprRows { get; } = [];
    public ObservableCollection<int> PprYears { get; } = [];
    public ObservableCollection<string> PprFilters { get; } = [];
    public ObservableCollection<PprTotalRow> PprTotals { get; } = [];

    [ObservableProperty] private int _pprYear;
    [ObservableProperty] private bool _pprByZone;
    [ObservableProperty] private string? _pprFilter = PprAllHouses;
    [ObservableProperty] private bool _pprOnlyOverdue;
    [ObservableProperty] private string _pprSummary = "";

    public bool CanEditPpr => shell.User is { } u && RoleCatalog.Can(u.Role, AppModule.Anten, out var level) && level == RoleCatalog.AccessLevel.Edit;
    public string PprCopyTitle => $"Скопировать план на {PprYear + 1}";

    /// <summary>Сменился пользователь: права на отметки.</summary>
    internal void RefreshRights() => OnPropertyChanged(nameof(CanEditPpr));

    partial void OnPprYearChanged(int value)
    {
        OnPropertyChanged(nameof(PprCopyTitle));
        RefreshPpr();
    }

    partial void OnPprByZoneChanged(bool value) => LoadPprFilters();
    partial void OnPprFilterChanged(string? value) => RefreshPpr();
    partial void OnPprOnlyOverdueChanged(bool value) => RefreshPpr();

    /// <summary>Годы, по которым есть план, и следующий за последним — чтобы было куда копировать.</summary>
    internal void LoadPprYears()
    {
        var years = shell.Houses.SelectMany(h => h.Ppr).Select(e => e.Year).Append(shell.Today.Year).Distinct().Order().ToList();
        years.Add(years[^1] + 1);
        PprYears.Clear();
        foreach (var y in years) PprYears.Add(y);
        if (PprYear == 0) PprYear = shell.Today.Year;
        LoadPprFilters();
    }

    private void LoadPprFilters()
    {
        var names = (PprByZone ? shell.Houses.Select(h => h.Zone) : shell.Houses.Select(h => h.Organization?.Name ?? ""))
            .Where(n => n != "").Distinct().Order().Prepend(PprAllHouses).ToList();
        if (!names.SequenceEqual(PprFilters))
        {
            var keep = PprFilter;
            PprFilters.Clear();
            foreach (var n in names) PprFilters.Add(n);
            // Список пересоздан и выпадающий список сбросил выбор: ставим его заново, даже если значение то же.
            PprFilter = null;
            PprFilter = names.Contains(keep ?? "") ? keep : PprAllHouses;
        }
        RefreshPpr();
    }

    private void RefreshPpr()
    {
        var houses = shell.Houses.Where(h => h.Serviced && MatchesPprFilter(h))
            .OrderBy(h => h.Street.Name).ThenBy(h => h.Number, NaturalOrder.Instance)
            .Select(h => new PprRow(h, PprYear, shell.Today))
            .Where(r => !PprOnlyOverdue || r.HasOverdue)
            .ToList();

        PprRows.Clear();
        foreach (var r in houses) PprRows.Add(r);

        // Итоги: сколько домов в плане месяца и сколько в них этажей, подъездов, квартир; последний столбец — за год.
        var months = Enumerable.Range(0, 12)
            .Select(m => houses.Where(r => r.Cells[m].Status != PprStatus.Empty).Select(r => r.House).ToList())
            .Append(houses.Where(r => r.Cells.Any(c => c.Status != PprStatus.Empty)).Select(r => r.House).ToList())
            .ToList();
        PprTotals.Clear();
        PprTotals.Add(new PprTotalRow("Домов в плане", months.Select(m => m.Count)));
        PprTotals.Add(new PprTotalRow("Этажей", months.Select(m => m.Sum(h => h.Floors))));
        PprTotals.Add(new PprTotalRow("Подъездов", months.Select(m => m.Sum(h => h.Entrances))));
        PprTotals.Add(new PprTotalRow("Квартир", months.Select(m => m.Sum(h => h.Apartments))));

        var entries = houses.SelectMany(r => r.House.Ppr.Where(e => e.Year == PprYear));
        var overdue = houses.Sum(r => r.Cells.Count(c => c.Status == PprStatus.Overdue));
        PprSummary = $"Выполнено на {shell.Today:dd.MM.yyyy}: {PprSchedule.DonePercent(entries, shell.Today)} %" +
                     (overdue > 0 ? $" · просрочено работ: {overdue}" : "");
    }

    private bool MatchesPprFilter(House h) =>
        PprFilter is null or PprAllHouses || (PprByZone ? h.Zone : h.Organization?.Name) == PprFilter;

    [RelayCommand]
    private async Task MarkPpr(PprCell? cell)
    {
        if (cell is null || !CanEditPpr) return;
        var status = PprSchedule.Next(cell.Status);
        await shell.Save(async () =>
        {
            await shell.Store.MarkPprAsync(cell.HouseId, cell.Year, cell.Month, status);
            await shell.ReloadAsync();
            shell.Status = $"ППР {cell.Address}, {shell.Months[cell.Month - 1].ToLowerInvariant()} {cell.Year}: {PprStatusText(status)}.";
        });
    }

    [RelayCommand]
    private async Task CopyPpr()
    {
        if (!CanEditPpr) return;
        var year = PprYear;
        await shell.Save(async () =>
        {
            var copied = await shell.Store.CopyPprAsync(year);
            await shell.ReloadAsync();
            PprYear = year + 1;
            shell.Status = $"План ППР перенесён на {year + 1} год: {copied} работ.";
        });
    }

    private static string PprStatusText(PprStatus status) => status switch
    {
        PprStatus.Planned => "в плане",
        PprStatus.Done => "выполнено",
        _ => "снято с плана"
    };
}

/// <summary>Месяц графика по дому: что в нём и куда отправить отметку.</summary>
public sealed record PprCell(int HouseId, string Address, int Year, int Month, PprStatus Status)
{
    public string Title => Billing.PprTitle(Status);
}

public sealed class PprRow
{
    public PprRow(House h, int year, DateOnly today)
    {
        House = h;
        var entries = h.Ppr.Where(e => e.Year == year).ToList();
        Cells = Enumerable.Range(1, 12)
            .Select(m => entries.FirstOrDefault(e => e.Month == m) is { } e
                ? new PprCell(h.Id, h.AddressLine, year, m, PprSchedule.Effective(e, today))
                : new PprCell(h.Id, h.AddressLine, year, m, PprStatus.Empty))
            .ToArray();
        var planned = Cells.Count(c => c.Status != PprStatus.Empty);
        Done = planned == 0 ? "" : $"{Cells.Count(c => c.Status == PprStatus.Done)} из {planned}";
    }

    public House House { get; }
    public string Address => House.AddressLine;
    public int Floors => House.Floors;
    public int Entrances => House.Entrances;
    public int Apartments => House.Apartments;
    public PprCell[] Cells { get; }
    public string Done { get; }
    public bool HasOverdue => Cells.Any(c => c.Status == PprStatus.Overdue);
}

/// <summary>Строка итогов под графиком: двенадцать месяцев и год.</summary>
public sealed class PprTotalRow(string title, IEnumerable<int> values)
{
    public string Title { get; } = title;
    public int[] Values { get; } = values.ToArray();
    public int Year => Values[12];
}
