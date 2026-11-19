using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Desktop.ViewModels;
using Avers.Desktop.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avers.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (Environment.GetEnvironmentVariable("AVERS_REDUCE_MOTION") != "1")
            Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://Avers/Styles/ButtonMotion.axaml") });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loading = new StartupWindow();
            var mode = ServerAddress.Choose(desktop.Args ?? []);
            loading.Opened += async (_, _) => await OpenWorkspaceAsync(desktop, loading, mode);
            desktop.MainWindow = loading;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task OpenWorkspaceAsync(
        IClassicDesktopStyleApplicationLifetime desktop, Window loading, StartupMode mode)
    {
        await Dispatcher.Yield(DispatcherPriority.Background);
        if (!loading.IsVisible) return;

        Window workspace;
        try
        {
            var demo = mode.Demo
                ? await Task.Run(() => OpenDemoDatabase(null, DateOnly.FromDateTime(DateTime.Now)))
                : null;
            if (!loading.IsVisible) return;
            workspace = CreateMainWindow(mode, demo);
        }
        catch (Exception exception)
        {
            if (!loading.IsVisible) return;
            System.Diagnostics.Trace.TraceError(exception.ToString());
            workspace = ProblemWindow("Не удалось открыть Аверс. Перезапустите приложение. Если ошибка повторится, обратитесь к администратору.");
        }

        desktop.MainWindow = workspace;
        workspace.Show();
        loading.Close();
    }

    /// <summary>
    /// С адресом сервера — общая база, с ключом --demo — учебная, иначе окно с сообщением (см. <see cref="ServerAddress"/>).
    /// </summary>
    public static Window CreateMainWindow(string[] args, string? directory = null) =>
        CreateMainWindow(ServerAddress.Choose(args, directory));

    private static Window CreateMainWindow(StartupMode mode, LocalStore? demo = null)
    {
        if (mode.Demo)
            return new MainWindow { DataContext = demo is null ? CreateMainViewModel() : CreateMainViewModel(demo) };
        if (mode.Server is not { } server)
            return ProblemWindow(mode.Problem!);

        var vm = CreateMainViewModel(new ServerStore(new HttpClient { BaseAddress = server, Timeout = TimeSpan.FromSeconds(30) }));
        vm.ShowDemoAccounts = mode.DemoAccounts;
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
