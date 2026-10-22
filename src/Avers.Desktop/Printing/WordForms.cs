using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Avers.Desktop.Printing;

/// <summary>
/// Письма юристов в Word (.docx): юристы правят текст под конкретное дело и печатают из Word.
/// Times New Roman 12, поля как в их образцах.
/// </summary>
public static class WordForms
{
    /// <summary>Досудебное уведомление: адрес справа, заголовок по центру, абзацы, подпись и исполнитель.</summary>
    public static void Claim(string address, string text, string path)
    {
        var lines = text.Replace("\r", "").Split('\n').Where(l => l.Trim() != "").ToList();
        Write(path, [$"Россия, {address}."], lines[0], lines.Skip(1),
            ["Зам. генерального директора\tН.С. Видяскина", "ООО «ПО «Аверс-Сервис»", "Исп. юридический отдел:", "т. (3812) 40-55-52, 38-40-11"]);
    }

    /// <summary>Заявление в суд или ОСП: шапка справа, текст, подпись директора и исполнитель.</summary>
    public static void Application(string[] header, string title, IEnumerable<string> body, string path) =>
        Write(path, header, title, body, ["Генеральный директор", "ООО «ПО «Аверс-Сервис»\tКоньшин С.А.", "Исп. юридический отдел: т. (3812) 40-55-52, 38-40-11"]);

    /// <summary>
    /// Договор и согласие на обработку персональных данных (<see cref="ContractForm"/>): шрифт 10, реквизиты сторон
    /// в две колонки, согласие с новой страницы.
    /// </summary>
    public static void Contract(ContractForm.Form f, string path)
    {
        const string size = "20";
        var parties = new Table(
            new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }),
            new TableRow(
                new TableCell(f.Company.Select(l => Para(l, size: size, after: "0"))),
                new TableCell(f.Subscriber.Select(l => Para(l, size: size, after: "0")))));
        Write(path,
        [
            Para(f.Title, JustificationValues.Center, bold: true, size: size),
            Para(f.Subtitle, JustificationValues.Center, size: size),
            Para($"{f.Place}\t{f.Date}", size: size),
            .. f.Sections.SelectMany(s => (s.Heading == "" ? [] : new[] { Para(s.Heading, JustificationValues.Center, bold: true, size: size) })
                .Concat(s.Paragraphs.Select(l => Para(l, JustificationValues.Both, size: size, after: "0")))),
            parties,
            new Paragraph(new Run(new Break { Type = BreakValues.Page })),
            .. f.ConsentHeader.Select(l => Para(l, JustificationValues.Right, size: size)),
            Para(f.ConsentTitle, JustificationValues.Center, bold: true, size: size),
            .. f.Consent.Select(l => Para(l, JustificationValues.Both, size: size, after: "0"))
        ]);
    }

    private static void Write(string path, IEnumerable<string> header, string title, IEnumerable<string> body, IEnumerable<string> signature) =>
        Write(path,
        [
            .. header.Select(l => Para(l, JustificationValues.Right)),
            Para(""),
            Para(title, JustificationValues.Center, bold: true),
            .. body.Select(l => Para(l, JustificationValues.Both, indent: true)),
            Para(""),
            .. signature.Select(l => Para(l))
        ]);

    private static void Write(string path, IEnumerable<OpenXmlElement> blocks)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var body = new Body(blocks);
        body.Append(new SectionProperties(
            new PageSize { Width = 11906, Height = 16838 },
            new PageMargin { Top = 1134, Bottom = 1134, Left = 1701, Right = 850 }));
        main.Document = new Document(body);
    }

    /// <summary>Абзац; табуляция в тексте уводит остаток строки к правому краю (подпись).</summary>
    private static Paragraph Para(string text, JustificationValues? align = null, bool bold = false, bool indent = false, string size = "24", string after = "120")
    {
        var props = new ParagraphProperties(
            new Tabs(new TabStop { Val = TabStopValues.Right, Position = 9355 }),
            new SpacingBetweenLines { After = after });
        if (indent) props.Append(new Indentation { FirstLine = "567" });
        if (align is { } a) props.Append(new Justification { Val = a });
        var font = new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" });
        if (bold) font.Append(new Bold());
        font.Append(new FontSize { Val = size });
        var run = new Run(font);
        var parts = text.Split('\t');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) run.Append(new TabChar());
            run.Append(new Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return new Paragraph(props, run);
    }
}
