using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;

namespace Avers.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var ru = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = ru;
        CultureInfo.DefaultThreadCurrentUICulture = ru;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { RenderingMode = RenderingModes() })
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// В сеансе удалённого рабочего стола видеокарты обычно нет, и DirectX эмулируется процессором сервера.
    /// Программная отрисовка там дешевле и не зависит от драйверов, поэтому для RDP берём сразу её.
    /// </summary>
    private static IReadOnlyList<Win32RenderingMode> RenderingModes() => IsRemoteSession()
        ? [Win32RenderingMode.Software]
        : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software];

    private static bool IsRemoteSession() => OperatingSystem.IsWindows() && GetSystemMetrics(SmRemoteSession) != 0;

    private const int SmRemoteSession = 0x1000;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
