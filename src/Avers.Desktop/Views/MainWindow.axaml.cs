using Avalonia.Controls;
using Avalonia.Interactivity;
using Avers.Desktop.ViewModels;

namespace Avers.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm && !vm.TryLeaveEdits(() => { Close(); return Task.CompletedTask; }))
            e.Cancel = true;
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e) => new AboutWindow { DataContext = DataContext }.ShowDialog(this);
}
