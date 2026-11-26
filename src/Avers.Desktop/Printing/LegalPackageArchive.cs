using System.IO.Compression;
using System.Text;
using Avers.Data;
using Avers.Domain;

namespace Avers.Desktop.Printing;

/// <summary>Расчётные материалы и договорные сканы в одном архиве без изменения исходных файлов.</summary>
public static class LegalPackageArchive
{
    public static async Task WriteAsync(string path, Subscriber subscriber, DateOnly today, bool debtNote, bool history,
        IReadOnlyList<DocumentInfo> contracts, Func<int, Task<byte[]?>> loadFile)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"пакет-{Guid.NewGuid():N}.tmp");
        var pdf = Path.Combine(directory, $"расчёт-{Guid.NewGuid():N}.pdf");
        try
        {
            PdfForms.CourtPackage(subscriber, today, debtNote, history, pdf);
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(pdf, "расчёт.pdf");
                foreach (var contract in contracts)
                {
                    var content = await loadFile(contract.Id) ?? throw new IOException($"Скан «{contract.Name}» удалён. Обновите список и повторите выгрузку.");
                    var entry = archive.CreateEntry($"сканы/договор-{contract.Id}{SubscriberDocuments.Extension(contract.Format)}");
                    using var stream = entry.Open();
                    await stream.WriteAsync(content);
                }
                using var writer = new StreamWriter(archive.CreateEntry("Состав.txt").Open(), new UTF8Encoding(false));
                await writer.WriteAsync(Composition(subscriber, today, debtNote, history, contracts));
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(pdf)) File.Delete(pdf);
        }
    }

    private static string Composition(Subscriber s, DateOnly today, bool debtNote, bool history, IReadOnlyList<DocumentInfo> contracts)
    {
        var lines = new List<string>
        {
            s.IsLegalEntity ? "Расчётный комплект организации" : "Пакет расчётных документов физлица",
            $"Дата: {today:dd.MM.yyyy}", $"Тип клиента: {s.ClientKind}", $"Клиент: {s.FullName}",
            $"Лицевой счёт: {s.Account}", $"Адрес обслуживания: г. Омск, {s.House.AddressLine}, кв. {s.Apartment}"
        };
        if (s.IsLegalEntity)
        {
            lines.AddRange([$"ИНН: {Known(s.Inn)}", $"КПП: {Known(s.Kpp)}", $"ОГРН: {Known(s.Ogrn)}",
                $"Юридический адрес: {Known(s.LegalAddress)}", $"Руководитель: {Known(s.Director)}"]);
        }
        lines.AddRange(["", "расчёт.pdf:"]);
        if (debtNote) lines.Add("- Справка о задолженности");
        if (history) lines.Add("- История платежей по годам");
        lines.Add("Договоры (сканы):");
        if (contracts.Count == 0) lines.Add("- Не приложены. Добавьте скан договора или включите его в состав перед выгрузкой.");
        foreach (var document in contracts)
            lines.Add($"- сканы/договор-{document.Id}{SubscriberDocuments.Extension(document.Format)}: {document.Name}");
        lines.Add("");
        lines.Add(s.IsLegalEntity
            ? "Согласованных юридических шаблонов для юрлиц пока нет. В комплекте только расчётные материалы и указанные сканы; заявления и неустойка по условиям договора физлица не формируются."
            : "Уведомление, заявление на судебный приказ и заявление в ОСП формируются отдельными кнопками. Перед подачей состав документов проверяет юрист.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Known(string value) => string.IsNullOrWhiteSpace(value) ? "не указан" : value;
}
