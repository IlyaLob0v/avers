using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>Запись графика ППР: отметка месяца по дому и перенос плана на следующий год.</summary>
public static class PprPlan
{
    /// <summary>Ставит статус месяца; отметка «выполнено» сдвигает дату последнего ППР в паспорте дома.</summary>
    public static void Mark(AversDbContext db, int houseId, int year, int month, PprStatus status, DateOnly today)
    {
        var house = db.Houses.First(h => h.Id == houseId);
        var entry = db.PprEntries.FirstOrDefault(e => e.HouseId == houseId && e.Year == year && e.Month == month);
        if (entry is null)
            db.PprEntries.Add(entry = new PprEntry { HouseId = houseId, Year = year, Month = month });
        entry.Status = status;

        if (status == PprStatus.Done)
        {
            var done = PprSchedule.DoneDate(year, month, today);
            if (house.LastPpr is null || house.LastPpr < done)
                house.LastPpr = done;
        }
        db.SaveChanges();
    }

    /// <summary>Копирует план года на следующий. Ноль — на следующий год план уже есть, ничего не тронуто.</summary>
    public static int CopyToNextYear(AversDbContext db, int year)
    {
        if (db.PprEntries.Any(e => e.Year == year + 1 && e.Status != PprStatus.Empty))
            return 0;
        var plan = PprSchedule.PlanForNextYear(db.PprEntries.AsNoTracking().Where(e => e.Year == year).ToList()).ToList();
        db.PprEntries.RemoveRange(db.PprEntries.Where(e => e.Year == year + 1));
        db.PprEntries.AddRange(plan);
        db.SaveChanges();
        return plan.Count;
    }
}
