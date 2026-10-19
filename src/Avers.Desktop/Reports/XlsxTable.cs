using ClosedXML.Excel;

namespace Avers.Desktop.Reports;

/// <summary>Отчёт в Excel: заголовок, строка шапки и таблица; ширина столбцов по содержимому.</summary>
public static class XlsxTable
{
    public static void Write(string path, string title, IReadOnlyList<string> headers, IEnumerable<object?[]> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Отчёт");
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 13;

        for (var c = 0; c < headers.Count; c++)
            sheet.Cell(3, c + 1).Value = headers[c];
        sheet.Range(3, 1, 3, headers.Count).Style.Font.Bold = true;

        var r = 4;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                switch (row[c])
                {
                    case decimal money:
                        cell.Value = money;
                        cell.Style.NumberFormat.Format = "#,##0.00";
                        break;
                    case int number:
                        cell.Value = number;
                        break;
                    default:
                        cell.Value = row[c]?.ToString() ?? "";
                        break;
                }
            }
            r++;
        }
        sheet.Columns(1, headers.Count).AdjustToContents(3);
        book.SaveAs(path);
    }
}
