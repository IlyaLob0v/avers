using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Avers.Desktop;
using Avers.Desktop.ViewModels;
using Avers.Desktop.Views;
using Avers.Domain;

// Снимки экранов Аверса без монитора: учебная база, вход под ролью, переход по модулям.
// Запуск: dotnet run --project tools/Avers.Screenshots -- <папка> [логин] [модуль ...] [--size=ШxВ]
// Пример: dotnet run --project tools/Avers.Screenshots -- shots admin Desktop Abon Disp --size=1366x768
// Без --size главное окно 1280×840, образцы оформления 1000×900; с --size оба окна этого размера.

var ru = CultureInfo.GetCultureInfo("ru-RU");
CultureInfo.DefaultThreadCurrentCulture = ru;
CultureInfo.DefaultThreadCurrentUICulture = ru;
CultureInfo.CurrentCulture = ru;
CultureInfo.CurrentUICulture = ru;

const string SizeOption = "--size=";
var sizeArg = args.FirstOrDefault(a => a.StartsWith(SizeOption, StringComparison.Ordinal));
Size? size = sizeArg is null ? null : ParseSize(sizeArg[SizeOption.Length..]);
args = args.Where(a => a != sizeArg).ToArray();

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
var window = new MainWindow { DataContext = vm, Width = size?.Width ?? 1280, Height = size?.Height ?? 840 };
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

var guide = new StyleGuideWindow { Width = size?.Width ?? 1000, Height = size?.Height ?? 900 };
guide.Show();
Save("99-obraztsy", guide);

File.Delete(dbPath);
File.Delete(dbPath + ".schema");
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

static Size ParseSize(string text)
{
    var parts = text.Split('x', 'X', '×');
    if (parts.Length == 2
        && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var width) && width > 0
        && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var height) && height > 0)
        return new Size(width, height);
    throw new ArgumentException($"Размер окна задаётся как --size=1366x768, а не «{text}»");
}
