using Avalonia;
using Avalonia.Controls;

namespace Avers.Desktop.Controls;

/// <summary>Цветовая пометка строки таблицы или плашки.</summary>
public enum RowTone
{
    None,
    /// <summary>Жёлтый: внимание, давно не было оплаты.</summary>
    Warn,
    /// <summary>Красный: долг, просрочка.</summary>
    Debt,
    /// <summary>Зелёный: оплачено, выполнено.</summary>
    Ok,
    /// <summary>Голубой: в плане, в работе.</summary>
    Info,
    /// <summary>Серый текст: снят с обслуживания, расторгнут, архив.</summary>
    Off
}

/// <summary>Строка таблицы, которая сама знает свой цвет.</summary>
public interface IRowTone
{
    RowTone Tone { get; }
}

/// <summary>
/// Красит строки DataGrid по <see cref="IRowTone.Tone"/>:
/// <c>&lt;DataGrid ctl:GridTone.Enabled="True" …&gt;</c>, где <c>xmlns:ctl="using:Avers.Desktop.Controls"</c>.
/// Строку, которая не знает свой цвет, красит <see cref="SelectorProperty"/>.
/// Цвет ставится, когда строка появляется на экране; после смены пометки обновите ItemsSource.
/// </summary>
public static class GridTone
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<DataGrid, bool>("Enabled", typeof(GridTone));

    /// <summary>Цвет строки по её данным, когда сами данные цвета не знают (например, заявка — просрочена ли на рабочую дату).</summary>
    public static readonly AttachedProperty<Func<object?, RowTone>?> SelectorProperty =
        AvaloniaProperty.RegisterAttached<DataGrid, Func<object?, RowTone>?>("Selector", typeof(GridTone));

    private static readonly string[] Classes = ["warn", "debt", "ok", "info", "off"];

    static GridTone()
    {
        EnabledProperty.Changed.AddClassHandler<DataGrid>((grid, e) =>
        {
            grid.LoadingRow -= OnLoadingRow;
            if (e.GetNewValue<bool>())
                grid.LoadingRow += OnLoadingRow;
        });
    }

    public static bool GetEnabled(DataGrid grid) => grid.GetValue(EnabledProperty);
    public static void SetEnabled(DataGrid grid, bool value) => grid.SetValue(EnabledProperty, value);
    public static Func<object?, RowTone>? GetSelector(DataGrid grid) => grid.GetValue(SelectorProperty);
    public static void SetSelector(DataGrid grid, Func<object?, RowTone>? value) => grid.SetValue(SelectorProperty, value);

    /// <summary>Имя класса стиля для пометки: warn, debt, ok, info, off или пустая строка.</summary>
    public static string ClassOf(RowTone tone) => tone == RowTone.None ? "" : Classes[(int)tone - 1];

    private static void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        var row = e.Row;
        foreach (var c in Classes)
            row.Classes.Remove(c);
        var tone = row.DataContext is IRowTone item ? item.Tone
            : sender is DataGrid grid && GetSelector(grid) is { } selector ? selector(row.DataContext)
            : RowTone.None;
        if (tone != RowTone.None)
            row.Classes.Add(ClassOf(tone));
    }
}
