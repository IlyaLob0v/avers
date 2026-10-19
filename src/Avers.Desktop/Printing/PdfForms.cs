using System.Globalization;
using Avers.Domain;
using SkiaSharp;

namespace Avers.Desktop.Printing;

/// <summary>
/// Печатные формы в PDF: квитанция и лицевой счёт абонента. PDF рисует SkiaSharp, который уже есть в Avalonia;
/// файл открывается программой просмотра PDF и печатается оттуда на любой принтер.
/// </summary>
public static class PdfForms
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Неоплаченные месяцы по текущий включительно — то, что идёт в квитанцию.</summary>
    public static List<YearPayment> DueUnpaid(Subscriber s, DateOnly today) =>
        s.Payments.Where(p => p.Amount is null && p.Year * 12 + p.Month <= today.Year * 12 + today.Month)
            .OrderBy(p => p.Year).ThenBy(p => p.Month).ToList();

    /// <summary>Квитанция на оплату ТО СКПТ: неоплаченные месяцы по текущий и сумма к оплате.</summary>
    public static void Receipt(Subscriber s, DateOnly today, string path)
    {
        var unpaid = DueUnpaid(s, today);
        using var pdf = new Page(path);
        pdf.Title("Квитанция на оплату технического обслуживания СКПТ");
        pdf.Field("Получатель", "");
        pdf.Field("Плательщик", s.FullName);
        pdf.Field("Адрес", Address(s));
        pdf.Field("Лицевой счёт", s.Account);
        pdf.Field("Индекс ПЖРЭУ", s.PzhreuIndex);
        pdf.Field("Договор", Contract(s));
        pdf.Gap();
        pdf.Row(true, ("Месяц", 0), ("Сумма, руб.", 420));
        foreach (var p in unpaid)
            pdf.Row(false, (Month(p), 0), (Money(p.Tariff), 420));
        pdf.Row(true, ("Итого к оплате", 0), (Money(unpaid.Sum(p => p.Tariff)), 420));
        pdf.Gap();
        pdf.Field("Дата", today.ToString("dd.MM.yyyy"));
        pdf.Field("Подпись плательщика", "");
    }

    /// <summary>Лицевой счёт за год: начислено, оплачено и, если нужно, дата оплаты по каждому месяцу.</summary>
    public static void Account(Subscriber s, int year, bool withPayDate, DateOnly today, string path)
    {
        var months = s.Payments.Where(p => p.Year == year).OrderBy(p => p.Month).ToList();
        using var pdf = new Page(path);
        pdf.Title($"Лицевой счёт № {s.Account} за {year} год");
        pdf.Field("Абонент", s.FullName);
        pdf.Field("Адрес", Address(s));
        pdf.Field("Договор", Contract(s));
        pdf.Gap();
        var head = new List<(string, float)> { ("Месяц", 0), ("Начислено, руб.", 170), ("Оплачено, руб.", 290) };
        if (withPayDate) head.Add(("Дата оплаты", 410));
        pdf.Row(true, [.. head]);
        foreach (var p in months)
        {
            var row = new List<(string, float)> { (Month(p), 0), (Money(p.Tariff), 170), (p.Amount is { } a ? Money(a) : "—", 290) };
            if (withPayDate) row.Add((p.PaidAt?.ToString("dd.MM.yyyy") ?? "", 410));
            pdf.Row(false, [.. row]);
        }
        pdf.Row(true, ("Итого", 0), (Money(Billing.Charged(months)), 170), (Money(Billing.Paid(months)), 290));
        pdf.Gap();
        pdf.Field("Задолженность", Money(Billing.Debt(months)) + " руб.");
        pdf.Field("Дата выписки", today.ToString("dd.MM.yyyy"));
    }

    private static string Address(Subscriber s) => $"{s.House.AddressLine}, кв. {s.Apartment}";

    private static string Contract(Subscriber s)
    {
        if (s.ContractDate is not { } d) return "нет";
        var number = s.ContractNumber == "" ? "" : $"№ {s.ContractNumber} ";
        return $"{number}от {d:dd.MM.yyyy}, {s.ContractParty}";
    }

    private static string Month(YearPayment p) => $"{Ru.DateTimeFormat.MonthNames[p.Month - 1].ToLowerInvariant()} {p.Year}";

    private static string Money(decimal value) => value.ToString("N2", Ru);

    /// <summary>Одна страница A4 с текстом сверху вниз.</summary>
    private sealed class Page : IDisposable
    {
        private const float Left = 50, Width = 495;
        private readonly FileStream _file;
        private readonly SKDocument _doc;
        private readonly SKCanvas _canvas;
        private readonly SKTypeface _regular = Typeface(SKFontStyle.Normal);
        private readonly SKTypeface _bold = Typeface(SKFontStyle.Bold);
        private readonly SKPaint _ink = new() { Color = SKColors.Black, IsAntialias = true };
        private float _y = 60;

        public Page(string path)
        {
            _file = File.Create(path);
            _doc = SKDocument.CreatePdf(_file);
            _canvas = _doc.BeginPage(595, 842);
        }

        public void Title(string text)
        {
            Text(text, Left, 14, _bold);
            _y += 30;
        }

        public void Field(string label, string value)
        {
            Text(label, Left, 11, _regular);
            Text(value, Left + 150, 11, _bold);
            _canvas.DrawLine(Left + 150, _y + 4, Left + Width, _y + 4, _ink);
            _y += 22;
        }

        public void Row(bool bold, params (string Text, float X)[] cells)
        {
            foreach (var (text, x) in cells)
                Text(text, Left + x, 11, bold ? _bold : _regular);
            _y += 18;
        }

        public void Gap() => _y += 12;

        private void Text(string text, float x, float size, SKTypeface face)
        {
            using var font = new SKFont(face, size);
            _canvas.DrawText(text, x, _y, font, _ink);
        }

        /// <summary>Шрифт с кириллицей: Arial на Windows, DejaVu Sans на Linux-сервере, иначе любой подходящий.</summary>
        private static SKTypeface Typeface(SKFontStyle style) =>
            new[] { "Arial", "DejaVu Sans", "Liberation Sans" }
                .Select(name => SKFontManager.Default.MatchFamily(name, style))
                .FirstOrDefault(f => f is not null)
            ?? SKFontManager.Default.MatchCharacter(null, style, null, 'Ж')
            ?? SKTypeface.Default;

        public void Dispose()
        {
            _doc.EndPage();
            _doc.Close();
            _doc.Dispose();
            _file.Dispose();
            _regular.Dispose();
            _bold.Dispose();
            _ink.Dispose();
        }
    }
}
