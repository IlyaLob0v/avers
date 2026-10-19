using Avers.Domain;

namespace Avers.Domain.Tests;

public class PprScheduleTests
{
    private static readonly DateOnly Today = new(2026, 8, 31);

    private static PprEntry Entry(int month, PprStatus status, int year = 2026) =>
        new() { HouseId = 1, Year = year, Month = month, Status = status };

    [Theory]
    [InlineData(7, PprStatus.Planned, PprStatus.Overdue)]
    [InlineData(8, PprStatus.Planned, PprStatus.Planned)]
    [InlineData(9, PprStatus.Planned, PprStatus.Planned)]
    [InlineData(3, PprStatus.Done, PprStatus.Done)]
    [InlineData(3, PprStatus.Empty, PprStatus.Empty)]
    public void Effective_PlanOfPastMonthIsOverdue(int month, PprStatus stored, PprStatus expected) =>
        Assert.Equal(expected, PprSchedule.Effective(Entry(month, stored), Today));

    [Fact]
    public void Effective_PlanOfLastYearIsOverdue() =>
        Assert.Equal(PprStatus.Overdue, PprSchedule.Effective(Entry(12, PprStatus.Planned, 2025), Today));

    [Theory]
    [InlineData(PprStatus.Empty, PprStatus.Planned)]
    [InlineData(PprStatus.Planned, PprStatus.Done)]
    [InlineData(PprStatus.Overdue, PprStatus.Done)]
    [InlineData(PprStatus.Done, PprStatus.Empty)]
    public void Next_CyclesThroughMarks(PprStatus from, PprStatus to) =>
        Assert.Equal(to, PprSchedule.Next(from));

    [Fact]
    public void DoneDate_IsTodayForCurrentMonthAndMonthEndForPast()
    {
        var midMonth = new DateOnly(2026, 8, 12);

        Assert.Equal(midMonth, PprSchedule.DoneDate(2026, 8, midMonth));
        Assert.Equal(new DateOnly(2026, 2, 28), PprSchedule.DoneDate(2026, 2, midMonth));
    }

    [Fact]
    public void DonePercent_CountsOnlyWorksDueByToday()
    {
        var entries = new[]
        {
            Entry(1, PprStatus.Done), Entry(4, PprStatus.Done), Entry(6, PprStatus.Planned),
            Entry(8, PprStatus.Planned), Entry(10, PprStatus.Planned), Entry(11, PprStatus.Empty)
        };

        Assert.Equal(50, PprSchedule.DonePercent(entries, Today));
        Assert.Equal(0, PprSchedule.DonePercent([], Today));
    }

    [Fact]
    public void PlanForNextYear_KeepsMonthsAndDropsDoneMarks()
    {
        var year = new[] { Entry(2, PprStatus.Done), Entry(5, PprStatus.Empty), Entry(9, PprStatus.Planned) };

        var next = PprSchedule.PlanForNextYear(year).ToList();

        Assert.Equal([2, 9], next.Select(e => e.Month));
        Assert.All(next, e => Assert.Equal((2027, PprStatus.Planned), (e.Year, e.Status)));
    }
}
