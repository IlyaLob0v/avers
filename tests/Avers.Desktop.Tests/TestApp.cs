using Avalonia;
using Avalonia.Headless;
using Avers.Desktop;

[assembly: AvaloniaTestApplication(typeof(Avers.Desktop.Tests.TestAppBuilder))]

// Все тесты окна идут через один поток Avalonia. Параллельно они вкладывают Pump() друг в друга,
// и на Windows (асинхронный файловый ввод-вывод, стек 1 МБ) процесс падал с переполнением стека.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Avers.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont();
}
