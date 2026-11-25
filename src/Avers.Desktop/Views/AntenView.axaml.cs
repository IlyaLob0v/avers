using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avers.Desktop.ViewModels;
using Avers.Domain;

namespace Avers.Desktop.Views;

public partial class AntenView : UserControl
{
    public AntenView()
    {
        InitializeComponent();
    }

    private void OnHouseDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.Source is not Control source) return;
        var row = source as DataGridRow ?? source.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not House house || !vm.OpenHouseCommand.CanExecute(house)) return;
        vm.OpenHouseCommand.Execute(house);
        e.Handled = true;
    }
}
