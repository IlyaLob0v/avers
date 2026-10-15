using Avalonia.Headless;
using Avalonia.Threading;
using Avers.Desktop.ViewModels;
using Avers.Desktop.Views;
using Microsoft.Data.Sqlite;

namespace Avers.Desktop.Tests;

/// <summary>
/// Главное окно Аверса без монитора (Avalonia.Headless) на своей временной учебной базе.
/// Окно показано по-настоящему: привязки таблиц и списков работают, как у сотрудника на экране.
/// </summary>
public sealed class DesktopApp : IDisposable
{
    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"avers-desktop-test-{Guid.NewGuid():N}.db");

    public DesktopApp(string login = "admin")
    {
        Vm = App.CreateMainViewModel(_databaseFile);
        Window = new MainWindow { DataContext = Vm, Width = 1280, Height = 840 };
        Window.Show();
        Vm.LoginUser = Vm.Users.Single(u => u.Login == login);
        Vm.Password = "avers";
        Vm.LoginCommand.Execute(null);
        Pump();
    }

    public MainViewModel Vm { get; }
    public MainWindow Window { get; }

    /// <summary>Дать окну обработать привязки и отрисоваться.</summary>
    public void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    public void Go(Avers.Domain.AppModule module)
    {
        Vm.GoCommand.Execute(module);
        Pump();
    }

    public void Dispose()
    {
        Window.Close();
        SqliteConnection.ClearAllPools();
        File.Delete(_databaseFile);
        File.Delete(_databaseFile + ".schema");
        File.Delete(_databaseFile + ".lock");
    }
}
