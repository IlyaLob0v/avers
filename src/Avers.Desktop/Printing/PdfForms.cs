using System.Globalization;
using Avers.Domain;
using SkiaSharp;

namespace Avers.Desktop.Printing;

/// <summary>
/// Печатные формы в PDF: квитанция и лицевой счёт абонента, досудебное уведомление и пакет для суда,
/// наряд-заказ радиомеханику, домофоны дома. PDF рисует SkiaSharp, который уже есть в Avalonia;
/// файл открывается программой просмотра PDF и печатается оттуда на любой принтер.
/// </summary>
public static class PdfForms
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Неоплаченные месяцы по текущий включительно — то, что идёт в квитанцию.</summary>
    public static List<YearPayment> DueUnpaid(Subscriber s, DateOnly today) =>
        Billing.Due(s.Payments, today).Where(p => p.Amount is null)
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
        using var pdf = new Page(path);
        Account(pdf, s, year, withPayDate, today);
    }

    /// <summary>
    /// Досудебное уведомление: текст с экрана, первая строка — заголовок. Знак ₽ заменяется на «руб.»:
    /// его нет в Arial на Windows 7.
    /// </summary>
    public static void Claim(string text, string path)
    {
        var lines = text.Replace("\r", "").Replace("₽", "руб.").Split('\n');
        using var pdf = new Page(path);
        pdf.Title(lines[0]);
        foreach (var paragraph in lines.Skip(1))
            pdf.Paragraph(paragraph);
    }

    /// <summary>
    /// Пакет для суда: справка о задолженности и (или) история платежей — лицевой счёт за каждый год с начислениями,
    /// каждый на своей странице.
    /// </summary>
    public static void CourtPackage(Subscriber s, DateOnly today, bool debtNote, bool history, string path)
    {
        using var pdf = new Page(path);
        if (debtNote)
            DebtNote(pdf, s, today);
        if (!history) return;
        var newPage = debtNote;
        foreach (var year in s.Payments.Select(p => p.Year).Distinct().Order())
        {
            if (newPage) pdf.NextPage();
            newPage = true;
            Account(pdf, s, year, true, today);
        }
    }

    private static void DebtNote(Page pdf, Subscriber s, DateOnly today)
    {
        var unpaid = DueUnpaid(s, today);
        pdf.Title("Справка о задолженности за техническое обслуживание СКПТ");
        pdf.Field("Абонент", s.FullName);
        pdf.Field("Адрес", Address(s));
        pdf.Field("Лицевой счёт", s.Account);
        pdf.Field("Договор", Contract(s));
        pdf.Gap();
        pdf.Row(true, ("Неоплаченный месяц", 0), ("Сумма, руб.", 420));
        foreach (var p in unpaid)
            pdf.Row(false, (Month(p), 0), (Money(p.Tariff), 420));
        pdf.Row(true, ("Задолженность", 0), (Money(Billing.Debt(Billing.Due(s.Payments, today))), 420));
        pdf.Gap();
        pdf.Field("Дата", today.ToString("dd.MM.yyyy"));
        pdf.Field("Подпись", "");
    }

    private static void Account(Page pdf, Subscriber s, int year, bool withPayDate, DateOnly today)
    {
        var months = s.Payments.Where(p => p.Year == year).OrderBy(p => p.Month).ToList();
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
        pdf.Field("Задолженность", Money(Billing.Debt(Billing.Due(months, today))) + " руб.");
        pdf.Field("Дата выписки", today.ToString("dd.MM.yyyy"));
    }

    /// <summary>
    /// Расчёт неустойки на дату по образцу юристов: долг, дни просрочки и неустойка по каждому месяцу;
    /// если неустойка больше долга, к взысканию — сумма долга (ст. 333 ГК РФ).
    /// </summary>
    public static void Penalty(Subscriber s, DateOnly date, string path)
    {
        var lines = Domain.Penalty.Calculate(s.Payments, date);
        var total = lines.Sum(l => l.Amount);
        var claimed = Domain.Penalty.Claimed(lines);
        using var pdf = new Page(path);
        pdf.Title($"Расчёт неустойки на {date:dd.MM.yyyy}");
        pdf.Field("Абонент", s.FullName);
        pdf.Field("Адрес", Address(s));
        pdf.Field("Договор", Contract(s));
        pdf.Gap();
        pdf.Paragraph($"Неустойка за месяц = долг за месяц × {Domain.Penalty.PercentPerDay:0} % × дни просрочки. Оплата — до 10 числа следующего месяца (п. 5.2 договора), неустойка — п. 2.3 договора.");
        pdf.Gap();
        pdf.Row(true, ("Месяц", 0), ("Долг, руб.", 150), ("Дней просрочки", 250), ("% в день", 360), ("Неустойка, руб.", 420));
        foreach (var l in lines)
            pdf.Row(false, (Month(l.Year, l.Month), 0), (Money(l.Debt), 150), (l.Days.ToString(Ru), 250),
                (Domain.Penalty.PercentPerDay.ToString("0", Ru), 360), (Money(l.Amount), 420));
        pdf.Row(true, ("Итого", 0), (Money(lines.Sum(l => l.Debt)), 150), (Money(total), 420));
        pdf.Gap();
        if (claimed < total)
            pdf.Paragraph("Сумма неустойки снижена до суммы основного долга в соответствии со ст. 333 ГК РФ.");
        pdf.Paragraph($"Таким образом, сумма неустойки, подлежащая взысканию с {s.FullName} по адресу: {Address(s)}, составляет {Money(claimed)} руб.");
        pdf.Gap();
        pdf.Field("Главный бухгалтер", "");
    }

    /// <summary>Домофоны дома: панели подъездов и невыполненные заявки — для радиомеханика.</summary>
    public static void Intercom(House house, IEnumerable<IntercomTicket> open, DateOnly today, string path)
    {
        using var pdf = new Page(path);
        pdf.Title($"Домофоны · {house.AddressLine}");
        foreach (var p in house.Intercoms)
        {
            pdf.Field($"Подъезд {p.Entrance}", p.PanelType);
            pdf.Field("Блок коммутации", p.SwitchBlock);
            pdf.Field("Ключи / ключницы", p.Keys);
            pdf.Field("Питание", p.Power);
            pdf.Gap();
        }
        pdf.Row(true, ("Невыполненные заявки", 0));
        pdf.Row(true, ("Дата", 0), ("Под.", 80), ("Неисправность", 120), ("Механик", 380));
        foreach (var t in open)
            pdf.Row(false, (t.Date.ToString("dd.MM.yyyy"), 0), (t.Entrance.ToString(Ru), 80), (t.Fault.Length > 45 ? t.Fault[..44] + "…" : t.Fault, 120), (t.Mechanic, 380));
        pdf.Gap();
        pdf.Field("Дата печати", today.ToString("dd.MM.yyyy"));
    }

    /// <summary>Наряд-заказ по заявке: радиомеханик берёт его на выезд и заполняет от руки, что сделано.</summary>
    public static void WorkOrder(WorkTicket t, string address, string path)
    {
        using var pdf = new Page(path);
        pdf.Title($"Наряд-заказ № {t.Number}");
        pdf.Field("Принята", $"{t.Date:dd.MM.yyyy} {t.Time:HH\\:mm}, {t.AcceptedBy}");
        pdf.Field("Адрес", address);
        pdf.Field("Телефон", t.Phone);
        pdf.Field("Вид работ", t.KindLabel);
        pdf.Field("Неисправность", t.Fault);
        pdf.Field("Радиомеханик", t.Mechanic);
        pdf.Gap();
        pdf.Field("Что произведено", t.WorkDone);
        pdf.Field("", "");
        pdf.Field("Материалы", "");
        pdf.Field("", "");
        pdf.Field("Дата выполнения", t.DoneDate?.ToString("dd.MM.yyyy") ?? "");
        pdf.Gap();
        pdf.Field("Подпись радиомеханика", "");
        pdf.Field("Подпись абонента", "");
    }

    private static string Address(Subscriber s) => $"{s.House.AddressLine}, кв. {s.Apartment}";

    private static string Contract(Subscriber s)
    {
        if (s.ContractDate is not { } d) return "нет";
        var number = s.ContractNumber == "" ? "" : $"№ {s.ContractNumber} ";
        return $"{number}от {d:dd.MM.yyyy}, {s.ContractParty}";
    }

    private static string Month(YearPayment p) => Month(p.Year, p.Month);

    private static string Month(int year, int month) => $"{Ru.DateTimeFormat.MonthNames[month - 1].ToLowerInvariant()} {year}";

    private static string Money(decimal value) => value.ToString("N2", Ru);

    /// <summary>Одна страница A4 с текстом сверху вниз.</summary>
    private sealed class Page : IDisposable
    {
        private const float Left = 50, Width = 495;
        private readonly FileStream _file;
        private readonly SKDocument _doc;
        private SKCanvas _canvas;
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

        /// <summary>Подпись слева, значение на линии справа; длинное значение переносится на следующие линии.</summary>
        public void Field(string label, string value)
        {
            Text(label, Left, 11, _regular);
            foreach (var line in Wrap(value, Width - 150, _bold))
            {
                Text(line, Left + 150, 11, _bold);
                _canvas.DrawLine(Left + 150, _y + 4, Left + Width, _y + 4, _ink);
                _y += 22;
            }
        }

        private static IEnumerable<string> Wrap(string value, float width, SKTypeface face)
        {
            using var font = new SKFont(face, 11);
            var line = "";
            foreach (var word in value.Split(' '))
            {
                var next = line == "" ? word : line + " " + word;
                if (line != "" && font.MeasureText(next) > width)
                {
                    yield return line;
                    next = word;
                }
                line = next;
            }
            yield return line;
        }

        public void Row(bool bold, params (string Text, float X)[] cells)
        {
            if (_y > 790) NextPage();
            foreach (var (text, x) in cells)
                Text(text, Left + x, 11, bold ? _bold : _regular);
            _y += 18;
        }

        public void Gap() => _y += 12;

        /// <summary>Абзац во всю ширину; пустая строка — отступ.</summary>
        public void Paragraph(string text)
        {
            foreach (var line in Wrap(text, Width, _regular))
            {
                Text(line, Left, 11, _regular);
                _y += 16;
            }
        }

        public void NextPage()
        {
            _doc.EndPage();
            _canvas = _doc.BeginPage(595, 842);
            _y = 60;
        }

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
