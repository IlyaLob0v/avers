using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avers.Desktop.Data;
using Avers.Desktop.ViewModels;

namespace Avers.Desktop.Views;

public partial class AbonView : UserControl
{
    public AbonView()
    {
        InitializeComponent();
    }

    private void OnSubscriberDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.Source is not Control source) return;
        var row = source as DataGridRow ?? source.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not SubscriberMatch match || !vm.OpenSearchMatchCommand.CanExecute(match)) return;
        vm.OpenSearchMatchCommand.Execute(match);
        e.Handled = true;
    }
}
