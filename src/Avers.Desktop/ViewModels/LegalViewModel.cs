using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avers.Contracts;
using Avers.Data;
using Avers.Desktop.Controls;
using Avers.Desktop.Data;
using Avers.Desktop.Printing;
using Avers.Desktop.Reports;
using Avers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avers.Desktop.ViewModels;

/// <summary>
/// Должники и судебная работа: список должников, калькулятор обратного отсчёта (этап, дата события, сколько дней осталось),
/// досудебное уведомление, судебное дело, пакет документов для суда и сканы документов должника.
/// </summary>
public sealed partial class LegalViewModel(MainViewModel shell) : ObservableObject
{
    public MainViewModel Shell => shell;

    public ObservableCollection<DebtorRow> Debtors { get; } = [];

    [ObservableProperty] private DebtorRow? _selectedDebtor;
    [ObservableProperty] private string _claimText = "";

    partial void OnSelectedDebtorChanged(DebtorRow? value)
    {
        BuildClaim();
        BindCourtCase();
        _ = LoadDocumentsAsync();
    }

    /// <summary>Новый список должников; выбранный остаётся выбранным, если он в списке.</summary>
    internal void ShowDebtors(IEnumerable<DebtorRow> rows)
    {
        var keep = SelectedDebtor?.SubscriberId;
        Debtors.Clear();
        foreach (var d in rows)
            Debtors.Add(d);
        SelectedDebtor = Debtors.FirstOrDefault(d => d.SubscriberId == keep) ?? Debtors.FirstOrDefault();
    }

    internal void BuildClaim()
    {
        var row = SelectedDebtor ?? Debtors.FirstOrDefault(d => d.Unpaid >= Billing.ClaimUnpaidMonths);
        ClaimText = row is null ? "" : ClaimNotice.Text(ClaimVariant.Kind, $"г. Омск, {row.Address}", row.Debt, row.ContractTerminated);
    }

    internal void RefreshRights() => OnPropertyChanged(nameof(CanEditCourtCases));

    public IReadOnlyList<LegalStage> LegalStages => LegalDeadline.Stages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LegalRemaining))]
    private LegalStage _legalStage = LegalDeadline.Stages[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LegalRemaining))]
    private DateTime? _legalStart;

    public string LegalRemaining => LegalStart is { } start
        ? LegalDeadline.Remaining(LegalDeadline.Due(LegalStage, DateOnly.FromDateTime(start)), shell.Today)
        : "";

    private int _caseId;
    [ObservableProperty] private string _caseOrder = "";
    [ObservableProperty] private string _caseDate = "";
    [ObservableProperty] private string _caseCourt = "";
    [ObservableProperty] private string _caseAmount = "";
    [ObservableProperty] private string _caseStatus = "";
    [ObservableProperty] private string _caseDuty = "";
    [ObservableProperty] private string _caseWritTo = "";
    [ObservableProperty] private string _caseWritDate = "";
    [ObservableProperty] private string _caseReceipts = "";
    [ObservableProperty] private string _caseNotes = "";

    public bool CanEditCourtCases => shell.User is { } u && RoleCatalog.Can(u.Role, AppModule.Legal, out var level) && level == RoleCatalog.AccessLevel.Edit;

    /// <summary>Последнее дело выбранного должника в полях формы; нет дела — пустая форма для нового.</summary>
    private void BindCourtCase()
    {
        var c = SelectedDebtor?.CourtCase;
        _caseId = c?.Id ?? 0;
        CaseOrder = c?.OrderNumber ?? "";
        CaseDate = c?.OrderDate.ToString("dd.MM.yyyy") ?? "";
        CaseCourt = c?.Court ?? "";
        CaseAmount = c?.Amount.ToString("0.00", MainViewModel.Ru) ?? SelectedDebtor?.Debt.ToString("0.00", MainViewModel.Ru) ?? "";
        CaseStatus = c?.Status ?? "";
        CaseDuty = c?.StateDuty.ToString("0.00", MainViewModel.Ru) ?? "";
        CaseWritTo = c?.WritSentTo ?? "";
        CaseWritDate = c?.WritSentDate?.ToString("dd.MM.yyyy") ?? "";
        CaseReceipts = c?.Receipts ?? "";
        CaseNotes = c?.Notes ?? "";
    }

    [RelayCommand]
    private void NewCourtCase()
    {
        if (SelectedDebtor is not { } d) return;
        _caseId = 0;
        CaseOrder = CaseCourt = CaseStatus = CaseDuty = CaseWritTo = CaseWritDate = CaseReceipts = CaseNotes = "";
        CaseDate = shell.Today.ToString("dd.MM.yyyy");
        CaseAmount = d.Debt.ToString("0.00", MainViewModel.Ru);
        shell.Status = $"Новое дело для {d.FullName}: заполните и нажмите «Сохранить дело».";
    }

    [RelayCommand]
    private async Task SaveCourtCase()
    {
        if (SelectedDebtor is not { } d || !CanEditCourtCases) return;
        if (!MainViewModel.TryParseDate(CaseDate, out var date) || date is null)
        {
            shell.Status = "Дата приказа — в виде дд.мм.гггг.";
            return;
        }
        if (!decimal.TryParse(CaseAmount.Trim(), System.Globalization.NumberStyles.Number, MainViewModel.Ru, out var amount))
        {
            shell.Status = "Сумма — в рублях, например 475 или 475,50.";
            return;
        }
        var duty = 0m;
        if (CaseDuty.Trim() != "" && !decimal.TryParse(CaseDuty.Trim(), System.Globalization.NumberStyles.Number, MainViewModel.Ru, out duty))
        {
            shell.Status = "Госпошлина — в рублях, например 200 или 200,50.";
            return;
        }
        if (!MainViewModel.TryParseDate(CaseWritDate, out var writDate))
        {
            shell.Status = "Дата направления листа — в виде дд.мм.гггг.";
            return;
        }
        await shell.Save(async () =>
        {
            await shell.Store.SaveCourtCaseAsync(d.SubscriberId, new CourtCase
            {
                Id = _caseId,
                OrderNumber = CaseOrder,
                OrderDate = date.Value,
                Court = CaseCourt,
                Amount = amount,
                Status = CaseStatus,
                StateDuty = duty,
                WritSentTo = CaseWritTo,
                WritSentDate = writDate,
                Receipts = CaseReceipts,
                Notes = CaseNotes
            });
            await shell.LoadDebtsAsync();
            shell.Status = $"Дело {d.FullName} сохранено.";
        });
    }

    [ObservableProperty] private bool _packageDebtNote = true;
    [ObservableProperty] private bool _packageHistory = true;

    public IReadOnlyList<ClaimVariant> ClaimVariants => ClaimNotice.Variants;

    [ObservableProperty] private ClaimVariant _claimVariant = ClaimNotice.Variants[0];

    partial void OnClaimVariantChanged(ClaimVariant value) => BuildClaim();

    /// <summary>Уведомление в Word: юристы правят текст и печатают оттуда.</summary>
    [RelayCommand]
    private void PrintClaim()
    {
        if (SelectedDebtor is not { } d) { shell.Status = "Выберите должника."; return; }
        shell.OpenWord(MainViewModel.PrintPath("уведомление", d.Account, ".docx"), $"уведомление {d.FullName}",
            path => WordForms.Claim($"г. Омск, {d.Address}", ClaimText, path));
    }

    /// <summary>
    /// Уведомления всем, у кого не оплачено от <see cref="Billing.ClaimUnpaidMonths"/> месяцев, одним файлом Word.
    /// Без тех, по кому уже есть судебный приказ, и без расторгнутых договоров: им текст выбирают по одному.
    /// </summary>
    [RelayCommand]
    private void PrintAllClaims()
    {
        var rows = Debtors.Where(d => d.Unpaid >= Billing.ClaimUnpaidMonths && d.Debt > 0 && d.CourtCase is null && d.ContractTerminated is null).ToList();
        if (rows.Count == 0) { shell.Status = $"Нет должников без судебного приказа с долгом от {Billing.ClaimUnpaidMonths} мес."; return; }
        shell.OpenWord(MainViewModel.PrintPath("уведомления", shell.Today.ToString("yyyy-MM-dd"), ".docx"), $"уведомлений: {rows.Count}",
            path => WordForms.Claims(rows.Select(d =>
            {
                var address = $"г. Омск, {d.Address}";
                return (address, ClaimNotice.Text(ClaimKind.Connected, address, d.Debt, null));
            }), path));
    }

    /// <summary>Пакет для суда по выбранному должнику: его оплаты грузятся вместе с домом.</summary>
    [RelayCommand]
    private async Task ExportCourtPackage()
    {
        if (SelectedDebtor is not { } d) { shell.Status = "Выберите должника."; return; }
        if (!PackageDebtNote && !PackageHistory) { shell.Status = "Отметьте, что положить в пакет."; return; }
        if (await LoadDebtorAsync(d) is not { } s) return;
        shell.OpenPdf(MainViewModel.PrintPath("пакет-для-суда", d.Account), $"пакет для суда {d.FullName}",
            path => PdfForms.CourtPackage(s, shell.Today, PackageDebtNote, PackageHistory, path));
    }

    [ObservableProperty] private DateTime? _penaltyDate;

    /// <summary>Расчёт неустойки на дату заявления; по умолчанию — на сегодня.</summary>
    [RelayCommand]
    private async Task PrintPenalty()
    {
        if (SelectedDebtor is not { } d) { shell.Status = "Выберите должника."; return; }
        var date = PenaltyDate is { } chosen ? DateOnly.FromDateTime(chosen) : shell.Today;
        if (await LoadDebtorAsync(d) is not { } s) return;
        shell.OpenPdf(MainViewModel.PrintPath("неустойка", d.Account), $"расчёт неустойки {d.FullName}", path => PdfForms.Penalty(s, date, path));
    }

    /// <summary>Заявление о выдаче судебного приказа в Word: долг и неустойка на ту же дату, что и расчёт неустойки.</summary>
    [RelayCommand]
    private async Task PrintCourtOrder()
    {
        var date = PenaltyDate is { } chosen ? DateOnly.FromDateTime(chosen) : shell.Today;
        await OpenApplication("заявление-на-приказ", "заявление на приказ", s => CourtOrderApplication.Text(s, date));
    }

    /// <summary>Заявление в ОСП в Word по последнему судебному делу должника.</summary>
    [RelayCommand]
    private Task PrintEnforcement() =>
        OpenApplication("заявление-в-осп", "заявление в ОСП", s => EnforcementApplication.Text(s, SelectedDebtor?.CourtCase));

    private async Task OpenApplication(string file, string title, Func<Subscriber, (string[] Header, string Title, string[] Body)> text)
    {
        if (SelectedDebtor is not { } d) { shell.Status = "Выберите должника."; return; }
        if (await LoadDebtorAsync(d) is not { } s) return;
        var (header, heading, body) = text(s);
        shell.OpenWord(MainViewModel.PrintPath(file, d.Account, ".docx"), $"{title} {d.FullName}",
            path => WordForms.Application(header, heading, body, path));
    }

    /// <summary>Абонент с оплатами: они грузятся вместе с домом.</summary>
    private async Task<Subscriber?> LoadDebtorAsync(DebtorRow d)
    {
        try
        {
            return (await shell.Store.LoadHouseAsync(shell.Houses.First(h => h.Id == d.HouseId))).First(x => x.Id == d.SubscriberId);
        }
        catch (StoreException e)
        {
            shell.Status = e.Message;
            return null;
        }
    }

    [RelayCommand]
    private void ExportDebtors() =>
        shell.Export("должники", "отчёт по должникам", path => XlsxTable.Write(path,
            $"Должники на {shell.Today:dd.MM.yyyy}",
            ["ФИО", "Адрес", "Лиц. счёт", "Долг, руб.", "Неоплачено мес.", "Последняя оплата", "Судебный приказ"],
            Debtors.Select(d => new object?[] { d.FullName, d.Address, d.Account, d.Debt, d.Unpaid, d.LastPay, d.Case })));

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
        if (shell.Module != AppModule.Claim || SelectedDebtor is not { } d) return;
        try
        {
            var list = await shell.Store.LoadDocumentsAsync(d.SubscriberId);
            if (d != SelectedDebtor) return;
            foreach (var doc in list)
                Documents.Add(doc);
        }
        catch (StoreException e)
        {
            shell.Status = e.Message;
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
            shell.Status = $"Не удалось прочитать файл: {e.Message}";
            return;
        }
        var name = Path.GetFileNameWithoutExtension(path);
        if (SubscriberDocuments.Check(DocumentKind, name, content) is { } error)
        {
            shell.Status = error;
            return;
        }
        await shell.Save(async () =>
        {
            await shell.Store.AddDocumentAsync(d.SubscriberId, new AddDocumentRequest(DocumentKind, name, content));
            await LoadDocumentsAsync();
            shell.Status = $"Скан «{name}» добавлен: {d.FullName}.";
        });
    }

    [RelayCommand]
    private async Task OpenDocument()
    {
        if (SelectedDocument is not { } doc) { shell.Status = "Выберите скан."; return; }
        try
        {
            if (await shell.Store.LoadDocumentFileAsync(doc.Id) is not { } content)
            {
                shell.Status = "Скан уже удалён.";
                return;
            }
            var dir = Path.Combine(Path.GetTempPath(), "Avers");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"скан-{doc.Id}{SubscriberDocuments.Extension(doc.Format)}");
            await File.WriteAllBytesAsync(path, content);
            shell.OpenFile(path);
            shell.Status = $"Открыт скан: {doc.Kind}, {doc.Name}.";
        }
        catch (StoreException e)
        {
            shell.Status = e.Message;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            shell.Status = $"Не удалось открыть скан: {e.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteDocument()
    {
        if (SelectedDocument is not { } doc) return;
        if (_deleteDocumentId != doc.Id)
        {
            _deleteDocumentId = doc.Id;
            shell.Status = $"Удалить скан «{doc.Name}»? Нажмите «Удалить скан» ещё раз.";
            return;
        }
        await shell.Save(async () =>
        {
            await shell.Store.DeleteDocumentAsync(doc.Id);
            await LoadDocumentsAsync();
            shell.Status = $"Скан «{doc.Name}» удалён.";
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

/// <summary>Строка списка должников: красная — долг от <see cref="Billing.DebtHighlight"/> ₽, жёлтая — давно не платил.</summary>
public sealed class DebtorRow : IRowTone
{
    public DebtorRow(Debtor d, House house)
    {
        SubscriberId = d.SubscriberId;
        HouseId = d.HouseId;
        Account = d.Account;
        FullName = d.FullName;
        Address = $"{house.AddressLine}, кв. {d.Apartment}";
        Debt = d.Debt;
        ContractTerminated = d.ContractTerminated;
        DebtText = d.Debt.ToString("N2") + " ₽";
        Unpaid = d.Unpaid;
        LastPay = d.LastPaymentDate is { } date ? date.ToString("dd.MM.yyyy") + (d.IsOld ? " (≥ 3 лет)" : "") : "нет";
        Limitation = d.LimitationEnds is { } end ? $"{d.OldestUnpaid:MM.yyyy} — до {end:dd.MM.yyyy}" : "";
        CourtCase = d.Case;
        Case = d.Case is { } c ? $"{c.OrderNumber} от {c.OrderDate:dd.MM.yyyy}" : "—";
        IsDebt = d.Debt >= Billing.DebtHighlight;
        IsOld = d.IsOld && !IsDebt;
    }
    public int SubscriberId { get; }
    public int HouseId { get; }
    public string Account { get; }
    public string FullName { get; }
    public string Address { get; }
    public decimal Debt { get; }
    public DateOnly? ContractTerminated { get; }
    public string DebtText { get; }
    public int Unpaid { get; }
    public string LastPay { get; }

    /// <summary>Самый старый неоплаченный месяц и последний день, когда его можно взыскать.</summary>
    public string Limitation { get; }
    public string Case { get; }

    /// <summary>Последнее судебное дело должника.</summary>
    public CourtCase? CourtCase { get; }
    public bool IsDebt { get; }
    public bool IsOld { get; }
    public RowTone Tone => IsDebt ? RowTone.Debt : IsOld ? RowTone.Warn : RowTone.None;
}
