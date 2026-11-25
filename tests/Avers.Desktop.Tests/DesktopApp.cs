using Avalonia.Headless;
using Avalonia.Threading;
using Avers.Data;
using Avers.Desktop.ViewModels;
using Avers.Desktop.Views;
using Avers.Domain;
using Microsoft.Data.Sqlite;

namespace Avers.Desktop.Tests;

/// <summary>
/// Главное окно Аверса без монитора (Avalonia.Headless) на своей временной учебной базе.
/// Окно показано по-настоящему: привязки таблиц и списков работают, как у сотрудника на экране.
/// </summary>
public sealed class DesktopApp : IDisposable
{
    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"avers-desktop-test-{Guid.NewGuid():N}.db");

    /// <param name="today">Рабочая дата; по умолчанию — дата учебной базы.</param>
    public DesktopApp(string login = "admin", DateOnly? today = null, bool selectRecord = true)
    {
        Vm = App.CreateMainViewModel(_databaseFile, today ?? DemoSeeder.WorkDate);
        Window = new MainWindow { DataContext = Vm, Width = 1280, Height = 840 };
        Window.Show();
        Vm.LoginUser = Vm.Users.Single(u => u.Login == login);
        Vm.Password = "avers";
        Vm.LoginCommand.Execute(null);
        Pump();
        if (selectRecord)
        {
            // Сценарии карточки явно выбирают учебную запись; программа при входе ничего не выбирает.
            Vm.SelectedStreet = Vm.Streets.FirstOrDefault(s => s.Name == DemoSeeder.StartStreet);
            Vm.SelectedHouse = Vm.HousesOnStreet.FirstOrDefault(h => h.Number == DemoSeeder.StartHouse);
            if (Vm.User is { } user && RoleCatalog.Can(user.Role, AppModule.Abon, out _))
            {
                Vm.SearchMode = SearchMode.Account;
                Vm.FindQuery = DemoSeeder.StartAccount;
                Vm.FindCommand.Execute(null);
                Pump();
                Vm.SearchMode = SearchMode.Address;
                Vm.FindStreet = DemoSeeder.StartStreet;
                Vm.FindHouse = DemoSeeder.StartHouse;
                Vm.FindSection = DemoSeeder.StartSection;
                Vm.FindApartment = DemoSeeder.StartApartment;
                Vm.FindQuery = "";
            }
            Vm.TicketsScreen.SelectedTicket = Vm.TicketsScreen.Tickets.FirstOrDefault(ticket => ticket.Number == DemoSeeder.StartTicket);
        }
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
        Vm.StayWithEditsCommand.Execute(null);
        Window.Close();
        if (Vm.HasPendingEditTransition)
        {
            Vm.DiscardEditsAndContinueCommand.Execute(null);
            Pump();
        }
        SqliteConnection.ClearAllPools();
        File.Delete(_databaseFile);
        File.Delete(_databaseFile + ".schema");
        File.Delete(_databaseFile + ".lock");
    }
}
