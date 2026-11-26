using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avers.Contracts;
using Avers.Data;
using Avers.Desktop.Data;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

public sealed partial class ContractsViewModel
{
    public ObservableCollection<DocumentInfo> ContractDocuments { get; } = [];
    [ObservableProperty] private DocumentInfo? _selectedContractDocument;
    [ObservableProperty] private string _contractDocumentsNotice = "Выберите договор в реестре.";
    private AppUser? _contractDocumentsUser;
    private int? _contractDocumentsSubscriberId;
    private int _contractDocumentsVersion;
    private int? _deleteContractDocumentId;

    public Func<Task<string?>> PickContractDocumentFile { get; set; } = PickContractScanAsync;
    public bool CanAddContractDocument => Shell.SelectedSubscriber is not null && Shell.User is { } user &&
        RoleCatalog.Can(user.Role, AppModule.Abon, out _) && (Shell.CanEditCard ||
            RoleCatalog.Can(user.Role, AppModule.Legal, out var level) && level == RoleCatalog.AccessLevel.Edit);
    private bool CanOpenContractDocument => SelectedContractDocument is not null;
    private bool CanDeleteContractDocument => CanAddContractDocument && SelectedContractDocument is not null;

    partial void OnSelectedContractDocumentChanged(DocumentInfo? value)
    {
        _deleteContractDocumentId = null;
        OpenContractDocumentCommand.NotifyCanExecuteChanged();
        DeleteContractDocumentCommand.NotifyCanExecuteChanged();
    }

    private void RefreshContractDocumentContext()
    {
        OnPropertyChanged(nameof(CanAddContractDocument));
        AddContractDocumentCommand.NotifyCanExecuteChanged();
        DeleteContractDocumentCommand.NotifyCanExecuteChanged();
        if (_contractDocumentsUser != Shell.User || _contractDocumentsSubscriberId != Shell.SelectedSubscriber?.Id)
            _ = RefreshContractDocuments();
    }

    [RelayCommand]
    private async Task RefreshContractDocuments()
    {
        var version = ++_contractDocumentsVersion;
        var user = _contractDocumentsUser = Shell.User;
        var subscriberId = _contractDocumentsSubscriberId = Shell.SelectedSubscriber?.Id;
        ContractDocuments.Clear();
        SelectedContractDocument = null;
        ContractDocumentsNotice = "Выберите договор в реестре.";
        if (user is null || subscriberId is null || !RoleCatalog.Can(user.Role, AppModule.Abon, out _)) return;
        ContractDocumentsNotice = "Загрузка сканов…";
        try
        {
            var documents = await Shell.Store.LoadContractDocumentsAsync(subscriberId.Value);
            if (version != _contractDocumentsVersion || !IsContractDocumentContext(user, subscriberId.Value)) return;
            foreach (var document in documents) ContractDocuments.Add(document);
            ContractDocumentsNotice = documents.Count == 0 ? "Скан бумажного договора ещё не добавлен."
                : "Сканы договоров этой квартиры. Выберите файл по номеру и дате; последний файл не выбирается автоматически.";
        }
        catch (StoreException e)
        {
            if (version == _contractDocumentsVersion && IsContractDocumentContext(user, subscriberId.Value))
                ContractDocumentsNotice = e.Message;
        }
    }

    private bool IsContractDocumentContext(AppUser user, int subscriberId) =>
        Shell.User == user && Shell.SelectedSubscriber?.Id == subscriberId;

    [RelayCommand(CanExecute = nameof(CanAddContractDocument))]
    private async Task AddContractDocument()
    {
        if (!Shell.TryLeaveEdits(AddContractDocument)) return;
        if (!CanAddContractDocument || Shell.User is not { } user || Shell.SelectedSubscriber is not { } subscriber) return;
        var path = await PickContractDocumentFile();
        if (path is null || !IsContractDocumentContext(user, subscriber.Id)) return;
        try
        {
            if (new FileInfo(path).Length > SubscriberDocuments.MaxBytes)
            {
                ContractDocumentsNotice = "Файл — не больше 20 МБ.";
                return;
            }
            var content = await File.ReadAllBytesAsync(path);
            if (!IsContractDocumentContext(user, subscriber.Id)) return;
            var number = subscriber.ContractNumber.Trim() is { Length: > 0 } n ? $" № {n}" : "";
            var date = subscriber.ContractDate is { } d ? $" от {d:dd.MM.yyyy}" : "";
            var name = $"Договор{number}{date} — {Path.GetFileNameWithoutExtension(path)}";
            if (name.Length > 200) name = name[..200];
            if (SubscriberDocuments.Check(SubscriberDocuments.ContractKind, name, content) is { } error)
            {
                ContractDocumentsNotice = error;
                return;
            }
            await Shell.Store.AddContractDocumentAsync(subscriber.Id, new AddDocumentRequest(SubscriberDocuments.ContractKind, name, content));
            if (IsContractDocumentContext(user, subscriber.Id)) await RefreshContractDocuments();
        }
        catch (Exception e) when (e is StoreException or IOException or UnauthorizedAccessException)
        {
            if (IsContractDocumentContext(user, subscriber.Id)) ContractDocumentsNotice = $"Не удалось добавить скан: {e.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenContractDocument))]
    private async Task OpenContractDocument()
    {
        if (Shell.User is not { } user || Shell.SelectedSubscriber is not { } subscriber || SelectedContractDocument is not { } document) return;
        try
        {
            var content = await Shell.Store.LoadContractDocumentFileAsync(document.Id);
            if (!IsContractDocumentContext(user, subscriber.Id) || SelectedContractDocument != document) return;
            if (content is null) { ContractDocumentsNotice = "Скан уже удалён. Обновите список."; return; }
            var directory = AversPaths.FilesDirectory;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"договор-скан-{document.Id}{SubscriberDocuments.Extension(document.Format)}");
            await File.WriteAllBytesAsync(path, content);
            if (IsContractDocumentContext(user, subscriber.Id) && SelectedContractDocument == document) Shell.OpenFile(path);
        }
        catch (Exception e) when (e is StoreException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            if (IsContractDocumentContext(user, subscriber.Id)) ContractDocumentsNotice = $"Не удалось открыть скан: {e.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteContractDocument))]
    private async Task DeleteContractDocument()
    {
        if (!CanDeleteContractDocument || Shell.User is not { } user || Shell.SelectedSubscriber is not { } subscriber || SelectedContractDocument is not { } document) return;
        if (_deleteContractDocumentId != document.Id)
        {
            _deleteContractDocumentId = document.Id;
            ContractDocumentsNotice = $"Удалить «{document.Name}»? Для подтверждения нажмите «Удалить скан» ещё раз.";
            return;
        }
        try
        {
            await Shell.Store.DeleteContractDocumentAsync(document.Id);
            if (IsContractDocumentContext(user, subscriber.Id)) await RefreshContractDocuments();
        }
        catch (StoreException e)
        {
            if (IsContractDocumentContext(user, subscriber.Id)) ContractDocumentsNotice = e.Message;
        }
    }

    private static async Task<string?> PickContractScanAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }) return null;
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Скан бумажного договора",
            FileTypeFilter = [new FilePickerFileType("PDF, JPG, PNG") { Patterns = ["*.pdf", "*.jpg", "*.jpeg", "*.png"] }]
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
