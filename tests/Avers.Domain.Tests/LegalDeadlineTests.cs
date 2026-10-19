using Avers.Domain;

namespace Avers.Domain.Tests;

public class LegalDeadlineTests
{
    private static LegalStage Stage(string title) => LegalDeadline.Stages.Single(s => s.Title == title);

    [Theory]
    [InlineData("Ответ на претензию", "2026-08-03", "2026-09-02")]          // среда
    [InlineData("Возражения на судебный приказ", "2026-08-05", "2026-08-17")] // 15.08 — суббота
    [InlineData("Обжалование решения суда", "2026-01-31", "2026-03-02")]     // 28.02 — суббота
    [InlineData("Срок исковой давности", "2023-08-31", "2026-08-31")]
    public void Due_CountsFromEventAndSkipsWeekend(string stage, string start, string due) =>
        Assert.Equal(DateOnly.Parse(due), LegalDeadline.Due(Stage(stage), DateOnly.Parse(start)));

    [Theory]
    [InlineData("Возражения на судебный приказ", "2026-12-22", "2027-01-11")] // 01.01–10.01 — каникулы
    [InlineData("Ответ на претензию", "2026-10-05", "2026-11-05")]           // 04.11 — праздник
    [InlineData("Возражения на судебный приказ", "2026-04-29", "2026-05-12")] // 09.05 перенесено на 11.05
    [InlineData("Возражения на судебный приказ", "2027-06-02", "2027-06-15")] // 12.06 перенесено на 14.06
    [InlineData("Возражения на судебный приказ", "2029-02-13", "2029-02-26")] // год без постановления, 23.02
    [InlineData("Возражения на судебный приказ", "2028-12-26", "2029-01-09")] // ст. 112: 1–8 января
    public void Due_SkipsHolidays(string stage, string start, string due) =>
        Assert.Equal(DateOnly.Parse(due), LegalDeadline.Due(Stage(stage), DateOnly.Parse(start)));

    [Fact]
    public void Due_KeepsWorkingSaturday() =>
        Assert.Equal(new DateOnly(2027, 2, 20), LegalDeadline.Due(Stage("Возражения на судебный приказ"), new DateOnly(2027, 2, 10)));

    [Fact]
    public void Remaining_SaysDaysLeftOrOverdue()
    {
        var due = new DateOnly(2026, 9, 2);

        Assert.Equal("осталось 2 дн. (до 02.09.2026)", LegalDeadline.Remaining(due, new DateOnly(2026, 8, 31)));
        Assert.Equal("последний день — сегодня, 02.09.2026", LegalDeadline.Remaining(due, due));
        Assert.Equal("срок истёк 02.09.2026, 3 дн. назад", LegalDeadline.Remaining(due, new DateOnly(2026, 9, 5)));
    }

    [Fact]
    public void Remaining_WarnsWhenYearHasNoTransfers() =>
        Assert.Equal("осталось 3 дн. (до 03.01.2029); переносы выходных на 2029 г. не внесены, проверьте по календарю",
            LegalDeadline.Remaining(new DateOnly(2029, 1, 3), new DateOnly(2028, 12, 31)));
}
