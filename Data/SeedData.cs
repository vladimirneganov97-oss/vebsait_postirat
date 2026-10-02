using LaundrySite.Models;

namespace LaundrySite.Data;

public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        var existing = db.WashSlots.ToList();
        // Convert the original 42-slot schedule to the new 35-slot schedule once.
        // After that, admin edits are preserved.
        if (existing.Any())
        {
            var isOldSchedule = existing.Count == 42 ||
                existing.Any(x => x.Time == "11:00–14:00" || x.Time == "02:00–03:00");
            if (!isOldSchedule) return;

            db.WashSlots.RemoveRange(existing);
            db.SaveChanges();
        }

        var days = new[] { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };
        var times = new[] { "08:00–11:00", "12:00–15:00", "16:00–19:00", "20:00–23:00", "00:00–03:00" };
        var slots = new List<WashSlot>();

        foreach (var day in days)
        foreach (var time in times)
            slots.Add(new WashSlot { Day = day, Time = time, Status = "Свободно" });

        db.WashSlots.AddRange(slots);
        db.SaveChanges();
    }
}
