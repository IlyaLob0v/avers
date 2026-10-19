using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avers.Desktop.ViewModels;
using Avers.Domain;

namespace Avers.Desktop.Views;

public partial class PprView : UserControl
{
    private static readonly string[] Months = ["Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек"];

    public PprView()
    {
        InitializeComponent();
        // Двенадцать месяцев: в графике — кнопки (щелчок меняет отметку, цвет — статус), в итогах — числа под ними.
        for (var i = 0; i < 12; i++)
        {
            var month = i;
            Plan.Columns.Insert(4 + i, new DataGridTemplateColumn
            {
                Header = Months[i],
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 52,
                CellTemplate = new FuncDataTemplate<PprRow>((row, _) => Cell(row?.Cells[month]), supportsRecycling: false)
            });
            Totals.Columns.Insert(1 + i, new DataGridTemplateColumn
            {
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 52,
                CellTemplate = new FuncDataTemplate<PprTotalRow>((row, _) =>
                    new TextBlock { Text = row?.Values[month].ToString(), Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center },
                    supportsRecycling: false)
            });
        }
    }

    private Button Cell(PprCell? cell)
    {
        var button = new Button { Content = cell?.Title };
        button.Classes.Add("ppr");
        switch (cell?.Status)
        {
            case PprStatus.Planned: button.Classes.Add("plan"); break;
            case PprStatus.Done: button.Classes.Add("done"); break;
            case PprStatus.Overdue: button.Classes.Add("late"); break;
        }
        if (DataContext is MainViewModel vm)
        {
            button.Command = vm.MarkPprCommand;
            button.CommandParameter = cell;
            button.IsHitTestVisible = vm.CanEditPpr;
        }
        return button;
    }
}
