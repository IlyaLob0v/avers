using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avers.Contracts;
using Avers.Data;
using Avers.Desktop.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Сканы документов должника на экране «Пакет для суда»: договор, решение суда, постановление ОСП.
/// Добавляет и удаляет тот, кто ведёт судебные дела; открываются программой просмотра Windows.
/// </summary>
public sealed partial class MainViewModel
{
    public ObservableCollection<DocumentInfo> Documents { get; } = [];
    public IReadOnlyList<string> DocumentKinds => SubscriberDocuments.Kinds;

    [ObservableProperty] private DocumentInfo? _selectedDocument;
    [ObservableProperty] private string _documentKind = SubscriberDocuments.Kinds[0];

    /// <summary>Скан, который попросили удалить: удаляется вторым нажатием «Удалить скан».</summary>
    private int? _deleteDocumentId;

    /// <summary>Выбор скана на диске; в тестах подменяется.</summary>
    public Func<Task<string?>> PickDocumentFile { get; set; } = PickDocumentAsync;

    partial void OnSelectedDocumentChanged(DocumentInfo? value) => _deleteDocumentId = null;

    private async Task LoadDocumentsAsync()
    {
        Documents.Clear();
        if (Module != Domain.AppModule.Claim || SelectedDebtor is not { } d) return;
        try
        {
            var list = await _store.LoadDocumentsAsync(d.SubscriberId);
            if (d != SelectedDebtor) return;
            foreach (var doc in list)
                Documents.Add(doc);
        }
        catch (StoreException e)
        {
            Status = e.Message;
        }
    }

    [RelayCommand]
    private async Task AddDocument()
    {
        if (SelectedDebtor is not { } d || await PickDocumentFile() is not { } path) return;
        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = $"Не удалось прочитать файл: {e.Message}";
            return;
        }
        var name = Path.GetFileNameWithoutExtension(path);
        if (SubscriberDocuments.Check(DocumentKind, name, content) is { } error)
        {
            Status = error;
            return;
        }
        await Save(async () =>
        {
            await _store.AddDocumentAsync(d.SubscriberId, new AddDocumentRequest(DocumentKind, name, content));
            await LoadDocumentsAsync();
            Status = $"Скан «{name}» добавлен: {d.FullName}.";
        });
    }

    [RelayCommand]
    private async Task OpenDocument()
    {
        if (SelectedDocument is not { } doc) { Status = "Выберите скан."; return; }
        try
        {
            if (await _store.LoadDocumentFileAsync(doc.Id) is not { } content)
            {
                Status = "Скан уже удалён.";
                return;
            }
            var dir = Path.Combine(Path.GetTempPath(), "Avers");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"скан-{doc.Id}{SubscriberDocuments.Extension(doc.Format)}");
            await File.WriteAllBytesAsync(path, content);
            OpenFile(path);
            Status = $"Открыт скан: {doc.Kind}, {doc.Name}.";
        }
        catch (StoreException e)
        {
            Status = e.Message;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Status = $"Не удалось открыть скан: {e.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteDocument()
    {
        if (SelectedDocument is not { } doc) return;
        if (_deleteDocumentId != doc.Id)
        {
            _deleteDocumentId = doc.Id;
            Status = $"Удалить скан «{doc.Name}»? Нажмите «Удалить скан» ещё раз.";
            return;
        }
        await Save(async () =>
        {
            await _store.DeleteDocumentAsync(doc.Id);
            await LoadDocumentsAsync();
            Status = $"Скан «{doc.Name}» удалён.";
        });
    }

    private static async Task<string?> PickDocumentAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return null;
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Скан документа",
            FileTypeFilter = [new FilePickerFileType("PDF, JPG, PNG") { Patterns = ["*.pdf", "*.jpg", "*.jpeg", "*.png"] }]
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
