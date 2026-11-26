using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avers.Desktop.ViewModels;
using Avers.Domain;

namespace Avers.Desktop.Views;

public partial class DispView : UserControl
{
    public DispView()
    {
        InitializeComponent();
    }

    private void OnTicketDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not TicketsViewModel screen || e.Source is not Control source) return;
        var row = source as DataGridRow ?? source.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not WorkTicket ticket) return;
        screen.JournalSelectedTicket = ticket;
        screen.OpenTicketCommand.Execute(null);
        e.Handled = true;
    }
}
