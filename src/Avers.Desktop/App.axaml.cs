using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.ViewModels;
using Avers.Desktop.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avers.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = CreateMainWindow(desktop.Args ?? []);

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// С адресом сервера — общая база, с ключом --demo — учебная, иначе окно с сообщением (см. <see cref="ServerAddress"/>).
    /// </summary>
    public static Window CreateMainWindow(string[] args, string? directory = null)
    {
        var mode = ServerAddress.Choose(args, directory);
        if (mode.Demo)
            return new MainWindow { DataContext = CreateMainViewModel() };
        if (mode.Server is not { } server)
            return ProblemWindow(mode.Problem!);

        var vm = CreateMainViewModel(new ServerStore(new HttpClient { BaseAddress = server, Timeout = TimeSpan.FromSeconds(30) }));
        if (ServerAddress.IsInsecure(server))
            vm.ConnectionWarning = "Связь с сервером не защищена: адрес начинается с http://. " +
                $"Попросите администратора указать в {ServerAddress.FileName} адрес https://.";
        return new MainWindow { DataContext = vm };
    }

    private static Window ProblemWindow(string message) => new()
    {
        Title = "Аверс",
        Width = 520,
        SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterScreen,
        Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) }
    };

    /// <summary>Модель главного окна на учебной базе. Нужна и приложению, и тестам со снимками экранов.</summary>
    public static MainViewModel CreateMainViewModel(string? databasePath = null, DateOnly? today = null) =>
        CreateMainViewModel(OpenDemoDatabase(databasePath, today ?? DateOnly.FromDateTime(DateTime.Now)));

    public static MainViewModel CreateMainViewModel(IAversStore store)
    {
        var vm = new MainViewModel(store);
        vm.Start();
        return vm;
    }

    private static LocalStore OpenDemoDatabase(string? databasePath, DateOnly today)
    {
        var services = new ServiceCollection();
        services.AddAversData(databasePath);
        var factory = services.BuildServiceProvider().GetRequiredService<IDbContextFactory<AversDbContext>>();

        var databaseFile = databasePath ?? AversPaths.DatabaseFile;
        using (DemoDatabase.Lock(databaseFile))
        using (var db = factory.CreateDbContext())
        {
            DemoDatabase.EnsureCurrent(db, databaseFile);
            DemoSeeder.EnsureSeeded(db);
        }
        return new LocalStore(factory, today);
    }
}
