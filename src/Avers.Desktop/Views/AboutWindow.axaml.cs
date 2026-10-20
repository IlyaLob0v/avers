using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Avers.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Версия {typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)}";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
