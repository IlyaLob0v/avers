using Avers.Desktop.Reports;
using ClosedXML.Excel;

namespace Avers.Desktop.Tests;

public class OekFileTests
{
    [Fact]
    public void Read_TakesFieldsByDbfHeaders()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nach082026_an-{Guid.NewGuid():N}.xlsx");
        using (var book = new XLWorkbook())
        {
            var sheet = book.AddWorksheet("Лист1");
            string[] headers = ["ACCOUNT,C,11", "LS,C,11", "ADDRESS,C,100", "NKWART,C,10", "DN,N,10,2", "NACH_AN,N,10,2",
                "TAR,N,8,2", "SUMMA_AN,N,10,2", "DATE_PAY,D"];
            for (var c = 0; c < headers.Length; c++)
                sheet.Cell(1, c + 1).Value = headers[c];
            sheet.Cell(2, 1).Value = "96010170082";
            sheet.Cell(2, 2).Value = "242236";
            sheet.Cell(2, 3).Value = "г. Омск, б-р. Мартынова д. 17, 8";
            sheet.Cell(2, 4).Value = "8";
            sheet.Cell(2, 6).Value = 150;
            sheet.Cell(2, 8).Value = 115.5;
            sheet.Cell(2, 9).Value = new DateTime(2026, 8, 14);
            sheet.Cell(3, 1).Value = "86054026476";
            book.SaveAs(path);
        }

        var rows = OekFile.Read(path);
        File.Delete(path);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new Avers.Data.OekRow("96010170082", "242236", "г. Омск, б-р. Мартынова д. 17, 8", "8", 150, 115.5m, new DateOnly(2026, 8, 14)), rows[0]);
        Assert.Equal(("", 0m, (DateOnly?)null), (rows[1].Account, rows[1].Paid, rows[1].PaidAt));
    }
}
