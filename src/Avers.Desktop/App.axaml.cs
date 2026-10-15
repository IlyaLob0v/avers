using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avers.Data;
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
            desktop.MainWindow = new MainWindow { DataContext = CreateMainViewModel() };

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Готовит учебную базу и модель главного окна. Нужен и приложению, и снимкам экранов.</summary>
    public static MainViewModel CreateMainViewModel(string? databasePath = null)
    {
        var services = new ServiceCollection();
        services.AddAversData(databasePath);
        services.AddSingleton<MainViewModel>();
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<AversDbContext>>();
        using (var db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
            DemoSeeder.EnsureSeeded(db);
        }

        var vm = provider.GetRequiredService<MainViewModel>();
        vm.Load();
        return vm;
    }
}
