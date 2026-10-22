using Avalonia.Headless.XUnit;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class UsersTests
{
    [AvaloniaFact]
    public void Access_AdministratorAddsEmployee_WhoAppearsInList()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Access);
        Assert.Contains(app.Vm.AccessScreen.Employees, e => e.Login == "orlova");

        app.Vm.AccessScreen.NewEmployeeCommand.Execute(null);
        app.Vm.AccessScreen.EditUserLogin = "petrova";
        app.Vm.AccessScreen.EditUserName = "Петрова А.А.";
        app.Vm.AccessScreen.EditUserRole = app.Vm.AccessScreen.RoleOptions.Single(r => r.Role == UserRole.Lawyer);
        app.Vm.AccessScreen.EditUserPassword = "secret1";
        app.Vm.AccessScreen.SaveEmployeeCommand.Execute(null);
        app.Pump();

        Assert.Equal("Сотрудник petrova заведён.", app.Vm.Status);
        Assert.Equal("Юрист", app.Vm.AccessScreen.SelectedEmployee?.RoleTitle);
    }

    [AvaloniaFact]
    public void Access_AdministratorCannotDisableSelf()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Access);

        app.Vm.AccessScreen.SelectedEmployee = app.Vm.AccessScreen.Employees.Single(e => e.Login == "admin");
        app.Vm.AccessScreen.EditUserDisabled = true;
        app.Vm.AccessScreen.SaveEmployeeCommand.Execute(null);
        app.Pump();

        Assert.Equal("Нельзя отключить себя или снять с себя роль администратора.", app.Vm.Status);
    }

    [AvaloniaFact]
    public void ChangeLog_ShowsEmployeeChange()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Access);
        app.Vm.AccessScreen.SelectedEmployee = app.Vm.AccessScreen.Employees.Single(e => e.Login == "orlova");
        app.Vm.AccessScreen.EditUserDepartment = "АДС";
        app.Vm.AccessScreen.SaveEmployeeCommand.Execute(null);
        app.Pump();

        app.Go(AppModule.ChangeLog);

        var row = Assert.Single(app.Vm.ChangeLogScreen.ChangeLogRows);
        Assert.Equal("Администратор (admin)", row.UserName);
        Assert.Equal("Сотрудник orlova", row.Target);
        Assert.Equal("Отдел: АДС, диспетчерская → АДС", row.Changes);
    }
}
