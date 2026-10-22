using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avers.Contracts;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Схемы СКПТ дома: просмотр картинки; добавление, замена файла, переименование и удаление (главный инженер, администратор).
/// </summary>
public sealed partial class SchemeViewModel(MainViewModel shell) : ObservableObject
{
    public ObservableCollection<HouseScheme> Schemes { get; } = [];

    [ObservableProperty] private HouseScheme? _selectedScheme;
    [ObservableProperty] private Bitmap? _schemeImage;
    [ObservableProperty] private string _schemeNote = "";
    [ObservableProperty] private string _schemeName = "";

    /// <summary>Схема, которую попросили удалить: удаляется вторым нажатием «Удалить».</summary>
    private int? _deleteSchemeId;

    public bool CanEditSchemes => shell.User is { } u && RoleCatalog.Can(u.Role, AppModule.Scheme, out var level) && level == RoleCatalog.AccessLevel.Edit;

    /// <summary>Выбор картинки на диске; в тестах подменяется.</summary>
    public Func<Task<string?>> PickSchemeFile { get; set; } = PickImageAsync;

    /// <summary>Сменился пользователь: права на правку схем.</summary>
    internal void RefreshRights() => OnPropertyChanged(nameof(CanEditSchemes));

    /// <summary>Выбран другой дом: его схемы, первая выбрана.</summary>
    internal void ShowHouse(House? house)
    {
        Schemes.Clear();
        if (house is not null)
            foreach (var s in house.Schemes) Schemes.Add(s);
        SelectedScheme = Schemes.FirstOrDefault();
    }

    partial void OnSelectedSchemeChanged(HouseScheme? value)
    {
        SchemeName = value?.Name ?? "";
        _deleteSchemeId = null;
        _ = ShowSchemeAsync();
    }

    /// <summary>Картинку читаем только на экране схем: файл может весить мегабайты.</summary>
    internal async Task ShowSchemeAsync()
    {
        if (shell.Module != AppModule.Scheme) return;
        var scheme = SelectedScheme;
        SetSchemeImage(null);
        SchemeNote = scheme is null ? "У дома нет схем." : "Загрузка…";
        if (scheme is null) return;
        try
        {
            var content = await shell.Store.LoadSchemeFileAsync(scheme.Id);
            if (scheme != SelectedScheme) return;
            SetSchemeImage(content is null ? null : new Bitmap(new MemoryStream(content)));
            SchemeNote = content is null ? "Картинка схемы не загружена. Главный инженер добавляет её кнопкой «Заменить файл»: JPG или PNG." : "";
        }
        catch (StoreException e)
        {
            SchemeNote = e.Message;
        }
    }

    private void SetSchemeImage(Bitmap? image)
    {
        var old = SchemeImage;
        SchemeImage = image;
        old?.Dispose();
    }

    [RelayCommand]
    private async Task AddScheme()
    {
        if (shell.SelectedHouse is not { } house || await ReadSchemeFile() is not { } file) return;
        await shell.Save(async () =>
        {
            await shell.Store.AddSchemeAsync(house.Id, new AddSchemeRequest(file.Name, file.Content));
            await shell.ReloadAsync();
            SelectedScheme = Schemes.LastOrDefault(s => s.Name == file.Name);
            shell.Status = $"Схема «{file.Name}» добавлена.";
        });
    }

    [RelayCommand]
    private async Task ReplaceSchemeFile()
    {
        if (SelectedScheme is not { } scheme || await ReadSchemeFile() is not { } file) return;
        await shell.Save(async () =>
        {
            await shell.Store.ReplaceSchemeFileAsync(scheme.Id, file.Content);
            await shell.ReloadAsync();
            SelectedScheme = Schemes.FirstOrDefault(s => s.Id == scheme.Id);
            shell.Status = $"Файл схемы «{scheme.Name}» заменён.";
        });
    }

    [RelayCommand]
    private async Task RenameScheme()
    {
        if (SelectedScheme is not { } scheme) return;
        if (SchemeFiles.CheckName(SchemeName) is { } error)
        {
            shell.Status = error;
            return;
        }
        var name = SchemeName.Trim();
        await shell.Save(async () =>
        {
            await shell.Store.RenameSchemeAsync(scheme.Id, name);
            await shell.ReloadAsync();
            SelectedScheme = Schemes.FirstOrDefault(s => s.Id == scheme.Id);
            shell.Status = $"Схема переименована: «{name}».";
        });
    }

    [RelayCommand]
    private async Task DeleteScheme()
    {
        if (SelectedScheme is not { } scheme) return;
        if (_deleteSchemeId != scheme.Id)
        {
            _deleteSchemeId = scheme.Id;
            shell.Status = $"Удалить схему «{scheme.Name}» вместе с картинкой? Нажмите «Удалить» ещё раз.";
            return;
        }
        await shell.Save(async () =>
        {
            await shell.Store.DeleteSchemeAsync(scheme.Id);
            await shell.ReloadAsync();
            shell.Status = $"Схема «{scheme.Name}» удалена.";
        });
    }

    /// <summary>Название схемы — имя файла без расширения.</summary>
    private async Task<(string Name, byte[] Content)?> ReadSchemeFile()
    {
        if (await PickSchemeFile() is not { } path) return null;
        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            shell.Status = $"Не удалось прочитать файл: {e.Message}";
            return null;
        }
        if (SchemeFiles.Check(content) is { } error)
        {
            shell.Status = error;
            return null;
        }
        return (Path.GetFileNameWithoutExtension(path), content);
    }

    private static async Task<string?> PickImageAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return null;
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Картинка схемы СКПТ",
            FileTypeFilter = [new FilePickerFileType("Картинки JPG, PNG") { Patterns = ["*.jpg", "*.jpeg", "*.png"] }]
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
