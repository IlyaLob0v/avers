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

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Handled || sender is not Control search || DataContext is not MainViewModel vm) return;
        if (search.GetVisualDescendants().OfType<AutoCompleteBox>().Any(box => box.IsDropDownOpen)) return;
        if (!vm.FindCommand.CanExecute(null)) return;
        vm.FindCommand.Execute(null);
        e.Handled = true;
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
