using Avalonia.Controls;
using Avers.Desktop.Controls;

namespace Avers.Desktop.Views;

/// <summary>Образцы общих элементов оформления: для разработчиков экранов и для согласования вида.</summary>
public partial class StyleGuideWindow : Window
{
    public StyleGuideWindow()
    {
        InitializeComponent();
        Samples.ItemsSource = SampleRow.All;
    }
}

public sealed record SampleRow(string Account, string Apartment, string Owner, string LastPayment, int MonthsSince, string Note, RowTone Tone) : IRowTone
{
    public static IReadOnlyList<SampleRow> All { get; } =
    [
        new("1932068", "68", "Иванова М.П.", "09.07.2026", 1, "оплата в срок", RowTone.None),
        new("1932041", "41", "Белоусов С.Н.", "12.03.2026", 5, "давно не платил — жёлтым", RowTone.Warn),
        new("1932012", "12", "Петров А.И.", "15.11.2025", 9, "долг, передан юристам", RowTone.Debt),
        new("1932090", "90", "Савельев И.Д.", "31.08.2026", 0, "оплачено за год", RowTone.Ok),
        new("1932101", "101", "Кузнецова О.В.", "—", 0, "заявка в работе", RowTone.Info),
        new("1932077", "77", "Смирнов П.П.", "01.02.2024", 31, "договор расторгнут", RowTone.Off),
    ];
}
