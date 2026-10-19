using System.Collections.ObjectModel;
using Avers.Desktop.Controls;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>Права доступа → сотрудники: администратор заводит, меняет роль и пароль, отключает.</summary>
public sealed partial class MainViewModel
{
    public ObservableCollection<EmployeeRow> Employees { get; } = [];

    public IReadOnlyList<RoleOption> RoleOptions { get; } = RoleCatalog.All.Select(r => new RoleOption(r.Role, r.Title)).ToList();

    [ObservableProperty] private EmployeeRow? _selectedEmployee;
    [ObservableProperty] private string _editUserLogin = "";
    [ObservableProperty] private string _editUserName = "";
    [ObservableProperty] private RoleOption? _editUserRole;
    [ObservableProperty] private string _editUserDepartment = "";
    [ObservableProperty] private bool _editUserCanEditAccount;
    [ObservableProperty] private bool _editUserDisabled;
    [ObservableProperty] private string _editUserPassword = "";

    public string EmployeeCaption => SelectedEmployee is null ? "Новый сотрудник" : $"Сотрудник {SelectedEmployee.User.Login}";

    public string PasswordHint => SelectedEmployee is null
        ? "Пароль обязателен, не короче 6 символов."
        : "Пусто — пароль не меняется.";

    /// <summary>Список сотрудников перечитываем при каждом входе в «Права доступа».</summary>
    private Task LoadEmployeesAsync() =>
        User?.Role == UserRole.Administrator ? Save(() => ReloadEmployeesAsync(SelectedEmployee?.Login)) : Task.CompletedTask;

    private async Task ReloadEmployeesAsync(string? selectLogin)
    {
        var users = await _store.LoadUsersAsync();
        Employees.Clear();
        foreach (var u in users)
            Employees.Add(new EmployeeRow(u));
        SelectedEmployee = Employees.FirstOrDefault(e => e.Login == selectLogin) ?? Employees.FirstOrDefault();
    }

    partial void OnSelectedEmployeeChanged(EmployeeRow? value)
    {
        var u = value?.User;
        EditUserLogin = u?.Login ?? "";
        EditUserName = u?.DisplayName ?? "";
        EditUserRole = RoleOptions.FirstOrDefault(r => r.Role == (u?.Role ?? UserRole.Dispatcher));
        EditUserDepartment = u?.Department ?? "";
        EditUserCanEditAccount = u?.CanEditAccount ?? false;
        EditUserDisabled = u?.IsDisabled ?? false;
        EditUserPassword = "";
        OnPropertyChanged(nameof(EmployeeCaption));
        OnPropertyChanged(nameof(PasswordHint));
    }

    [RelayCommand]
    private void NewEmployee() => SelectedEmployee = null;

    [RelayCommand]
    private async Task SaveEmployee()
    {
        if (User?.Role != UserRole.Administrator)
        {
            Status = "Сотрудников заводит администратор.";
            return;
        }
        if (EditUserRole is null)
        {
            Status = "Выберите роль.";
            return;
        }
        var draft = new AppUser
        {
            Id = SelectedEmployee?.User.Id ?? 0,
            Login = EditUserLogin,
            DisplayName = EditUserName,
            Role = EditUserRole.Role,
            Department = EditUserDepartment,
            CanEditAccount = EditUserCanEditAccount,
            IsDisabled = EditUserDisabled
        };
        var passwordChanged = EditUserPassword.Length > 0;
        await Save(async () =>
        {
            await _store.SaveUserAsync(draft, EditUserPassword, User!);
            var login = draft.Login.Trim();
            await ReloadEmployeesAsync(login);
            Status = draft.Id == 0 ? $"Сотрудник {login} заведён."
                : passwordChanged ? $"Сотрудник {login} сохранён, пароль изменён."
                : $"Сотрудник {login} сохранён.";
        });
    }
}

public sealed record RoleOption(UserRole Role, string Title);

public sealed class EmployeeRow(AppUser user) : IRowTone
{
    public AppUser User { get; } = user;
    public string Login => User.Login;
    public string DisplayName => User.DisplayName;
    public string RoleTitle => RoleCatalog.Title(User.Role);
    public string Department => User.Department;
    public string Account => User.CanEditAccount ? "да" : "";
    public string State => User.IsDisabled ? "отключён" : "";
    public RowTone Tone => User.IsDisabled ? RowTone.Off : RowTone.None;
}
