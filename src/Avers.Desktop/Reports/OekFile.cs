using System.Globalization;
using Avers.Data;
using ClosedXML.Excel;

namespace Avers.Desktop.Reports;

/// <summary>
/// Файл начислений ОЭК (nachMMYYYY_an, DBF, сохранённый в Excel): первая строка — поля DBF вида «ACCOUNT,C,11».
/// Нужны ACCOUNT, LS, ADDRESS, NKWART, NACH_AN, SUMMA_AN и DATE_PAY.
/// </summary>
public static class OekFile
{
    private static readonly string[] Required = ["ACCOUNT", "LS", "ADDRESS", "NKWART", "NACH_AN", "SUMMA_AN", "DATE_PAY"];

    /// <summary>Строки файла; если какого-то поля нет — <see cref="InvalidDataException"/> с его названием.</summary>
    public static List<OekRow> Read(string path)
    {
        using var book = new XLWorkbook(path);
        var sheet = book.Worksheet(1);
        var columns = new Dictionary<string, int>();
        foreach (var cell in sheet.Row(1).CellsUsed())
            columns.TryAdd(cell.GetString().Split(',')[0].Trim().ToUpperInvariant(), cell.Address.ColumnNumber);
        var missing = Required.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"В файле нет полей: {string.Join(", ", missing)}. Нужен файл начислений ОЭК (nachMMYYYY_an).");

        var rows = new List<OekRow>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            string Text(string c) => row.Cell(columns[c]).GetFormattedString().Trim();
            // Суммы и даты бывают и текстом («115,50», «14.08.2026»): запятая — десятичная, а не разделитель тысяч.
            decimal Money(string c) => row.Cell(columns[c]).DataType == XLDataType.Number
                ? row.Cell(columns[c]).GetValue<decimal>()
                : decimal.TryParse(Text(c).Replace(" ", "").Replace("\u00A0", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0;
            var account = Text("ACCOUNT");
            if (account == "")
                continue;
            var paidAt = row.Cell(columns["DATE_PAY"]).TryGetValue(out DateTime d) ? DateOnly.FromDateTime(d)
                : DateOnly.TryParseExact(Text("DATE_PAY"), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : (DateOnly?)null;
            rows.Add(new OekRow(account, Text("LS"), Text("ADDRESS"), Text("NKWART"), Money("NACH_AN"), Money("SUMMA_AN"), paidAt));
        }
        return rows;
    }
}
