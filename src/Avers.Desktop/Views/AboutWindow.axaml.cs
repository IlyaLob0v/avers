using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Avers.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var assembly = typeof(AboutWindow).Assembly;
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var parts = info?.Split('+');
        var revision = parts is { Length: > 1 } ? $" · сборка {parts[1][..Math.Min(7, parts[1].Length)]}" : "";
        VersionText.Text = $"Версия {assembly.GetName().Version?.ToString(3)}{revision}";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
