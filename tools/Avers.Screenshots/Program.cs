using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Avers.Desktop;
using Avers.Desktop.ViewModels;
using Avers.Desktop.Views;
using Avers.Domain;

// Снимки экранов Аверса без монитора: учебная база, вход под ролью, переход по модулям.
// Запуск: dotnet run --project tools/Avers.Screenshots -- <папка> [логин] [модуль ...]
// Пример: dotnet run --project tools/Avers.Screenshots -- shots admin Desktop Abon Disp

var ru = CultureInfo.GetCultureInfo("ru-RU");
CultureInfo.DefaultThreadCurrentCulture = ru;
CultureInfo.DefaultThreadCurrentUICulture = ru;
CultureInfo.CurrentCulture = ru;
CultureInfo.CurrentUICulture = ru;

var outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "screenshots");
var login = args.Length > 1 ? args[1] : "admin";
var modules = args.Length > 2
    ? args.Skip(2).Select(Enum.Parse<AppModule>).ToArray()
    : [AppModule.Desktop, AppModule.Abon, AppModule.Subscriber, AppModule.Disp, AppModule.Anten, AppModule.Contract, AppModule.Legal];
Directory.CreateDirectory(outDir);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

var dbPath = Path.Combine(Path.GetTempPath(), $"avers-shots-{Guid.NewGuid():N}.db");
var vm = App.CreateMainViewModel(dbPath);
var window = new MainWindow { DataContext = vm, Width = 1280, Height = 840 };
window.Show();

Save("00-vhod");

vm.LoginUser = vm.Users.FirstOrDefault(u => u.Login == login)
    ?? throw new InvalidOperationException($"Нет пользователя {login}");
vm.Password = "avers";
vm.LoginCommand.Execute(null);

var n = 1;
foreach (var module in modules)
{
    vm.GoCommand.Execute(module);
    Save($"{n++:00}-{module.ToString().ToLowerInvariant()}");
}

var guide = new StyleGuideWindow { Width = 1000, Height = 900 };
guide.Show();
Save("99-obraztsy", guide);

File.Delete(dbPath);
return;

void Save(string name, Avalonia.Controls.Window? target = null)
{
    target ??= window;
    // DataGrid и ScrollViewer досчитывают раскладку за несколько проходов.
    for (var i = 0; i < 5; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
    Dispatcher.UIThread.RunJobs();
    var frame = target.CaptureRenderedFrame() ?? throw new InvalidOperationException("Кадр не отрисован");
    var file = Path.Combine(outDir, $"{name}.png");
    frame.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    Console.WriteLine(file);
}
