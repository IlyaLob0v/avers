using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.MainWindow = new MainWindow { DataContext = CreateMainViewModel(desktop.Args ?? []) };

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>С адресом сервера — общая база, без него — учебная (см. <see cref="ServerAddress"/>).</summary>
    private static MainViewModel CreateMainViewModel(string[] args)
    {
        Uri? server;
        try
        {
            server = ServerAddress.Find(args);
        }
        catch (InvalidOperationException e)
        {
            var vm = CreateMainViewModel();
            vm.LoginError = e.Message + " Открыта учебная база.";
            return vm;
        }
        return server is null
            ? CreateMainViewModel()
            : CreateMainViewModel(new ServerStore(new HttpClient { BaseAddress = server, Timeout = TimeSpan.FromSeconds(30) }));
    }

    /// <summary>Модель главного окна на учебной базе. Нужна и приложению, и тестам со снимками экранов.</summary>
    public static MainViewModel CreateMainViewModel(string? databasePath = null) =>
        CreateMainViewModel(OpenDemoDatabase(databasePath));

    public static MainViewModel CreateMainViewModel(IAversStore store)
    {
        var vm = new MainViewModel(store);
        vm.Start();
        return vm;
    }

    private static LocalStore OpenDemoDatabase(string? databasePath)
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
        return new LocalStore(factory);
    }
}
