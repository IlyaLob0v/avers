using Avalonia;
using Avalonia.Headless;
using Avers.Desktop;

[assembly: AvaloniaTestApplication(typeof(Avers.Desktop.Tests.TestAppBuilder))]

namespace Avers.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont();
}
