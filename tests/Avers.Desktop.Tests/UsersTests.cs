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
        Assert.Contains(app.Vm.Employees, e => e.Login == "orlova");

        app.Vm.NewEmployeeCommand.Execute(null);
        app.Vm.EditUserLogin = "petrova";
        app.Vm.EditUserName = "Петрова А.А.";
        app.Vm.EditUserRole = app.Vm.RoleOptions.Single(r => r.Role == UserRole.Lawyer);
        app.Vm.EditUserPassword = "secret1";
        app.Vm.SaveEmployeeCommand.Execute(null);
        app.Pump();

        Assert.Equal("Сотрудник petrova заведён.", app.Vm.Status);
        Assert.Equal("Юрист", app.Vm.SelectedEmployee?.RoleTitle);
    }

    [AvaloniaFact]
    public void Access_AdministratorCannotDisableSelf()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Access);

        app.Vm.SelectedEmployee = app.Vm.Employees.Single(e => e.Login == "admin");
        app.Vm.EditUserDisabled = true;
        app.Vm.SaveEmployeeCommand.Execute(null);
        app.Pump();

        Assert.Equal("Нельзя отключить себя или снять с себя роль администратора.", app.Vm.Status);
    }

    [AvaloniaFact]
    public void ChangeLog_ShowsEmployeeChange()
    {
        using var app = new DesktopApp();
        app.Go(AppModule.Access);
        app.Vm.SelectedEmployee = app.Vm.Employees.Single(e => e.Login == "orlova");
        app.Vm.EditUserDepartment = "АДС";
        app.Vm.SaveEmployeeCommand.Execute(null);
        app.Pump();

        app.Go(AppModule.ChangeLog);

        var row = Assert.Single(app.Vm.ChangeLogRows);
        Assert.Equal("Администратор (admin)", row.UserName);
        Assert.Equal("Сотрудник orlova", row.Target);
        Assert.Equal("Отдел: АДС, диспетчерская → АДС", row.Changes);
    }
}
