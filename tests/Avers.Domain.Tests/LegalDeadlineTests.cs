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

    [Fact]
    public void Remaining_SaysDaysLeftOrOverdue()
    {
        var due = new DateOnly(2026, 9, 2);

        Assert.Equal("осталось 2 дн. (до 02.09.2026)", LegalDeadline.Remaining(due, new DateOnly(2026, 8, 31)));
        Assert.Equal("последний день — сегодня, 02.09.2026", LegalDeadline.Remaining(due, due));
        Assert.Equal("срок истёк 02.09.2026, 3 дн. назад", LegalDeadline.Remaining(due, new DateOnly(2026, 9, 5)));
    }
}
