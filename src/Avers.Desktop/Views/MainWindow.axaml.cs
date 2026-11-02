using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Avers.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e) => new AboutWindow { DataContext = DataContext }.ShowDialog(this);
}
