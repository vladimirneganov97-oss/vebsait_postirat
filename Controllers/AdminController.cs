using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LaundrySite.Data;
using LaundrySite.Models;

namespace LaundrySite.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();

    public AdminController(AppDbContext db) => _db = db;

    private bool IsAdmin => HttpContext.Session.GetString("Admin") == "true";

    private static DateTime WeekStart(DateTime date)
    {
        var d = date.Date;
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAdmin) return RedirectToAction(nameof(Login));

        var week = WeekStart(DateTime.Now);
        var users = await _db.Users.OrderByDescending(x => x.CreatedAt).ToListAsync();
        var slots = await _db.WashSlots.OrderBy(x => x.Id).ToListAsync();

        // Repair old reservations that were stored only as text.
        foreach (var slot in slots.Where(s => s.ReservationWeekStart?.Date == week.Date && s.ReservedByUserId == null && !string.IsNullOrWhiteSpace(s.ReservedBy)))
        {
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                (u.FirstName + " " + u.LastName + " (комната " + u.RoomNumber + ")") == slot.ReservedBy);
            if (user != null) slot.ReservedByUserId = user.Id;
        }
        await _db.SaveChangesAsync();

        var counts = await _db.WashSlots
            .Where(s => s.ReservationWeekStart != null && s.ReservationWeekStart.Value.Date == week.Date && s.ReservedByUserId != null)
            .GroupBy(s => s.ReservedByUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        ViewBag.Users = users;
        ViewBag.Slots = slots;
        ViewBag.WeeklyCounts = counts;
        ViewBag.WeekStart = week;
        return View();
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(string password)
    {
        var adminPassword = Environment.GetEnvironmentVariable("LAUNDRY_ADMIN_PASSWORD");
        if (string.IsNullOrEmpty(adminPassword))
        {
            ViewBag.Error = "Пароль администратора не настроен. Задайте LAUNDRY_ADMIN_PASSWORD.";
            return View();
        }

        var supplied = Encoding.UTF8.GetBytes(password ?? "");
        var expected = Encoding.UTF8.GetBytes(adminPassword);
        var valid = supplied.Length == expected.Length &&
                    CryptographicOperations.FixedTimeEquals(supplied, expected);

        if (valid)
        {
            HttpContext.Session.SetString("Admin", "true");
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Error = "Неверный пароль.";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!IsAdmin) return RedirectToAction(nameof(Login));

        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            foreach (var slot in _db.WashSlots.Where(s => s.ReservedByUserId == user.Id))
            {
                slot.ReservedBy = null;
                slot.ReservedByUserId = null;
                slot.ReservationWeekStart = null;
            }

            // Compatibility with reservations from older versions.
            var identity = $"{user.FirstName} {user.LastName} (комната {user.RoomNumber})";
            foreach (var slot in _db.WashSlots.Where(s => s.ReservedBy == identity))
            {
                slot.ReservedBy = null;
                slot.ReservedByUserId = null;
                slot.ReservationWeekStart = null;
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserLimit(int id, int weeklyBookingLimit)
    {
        if (!IsAdmin) return RedirectToAction(nameof(Login));

        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            user.WeeklyBookingLimit = Math.Clamp(weeklyBookingLimit, 0, 50);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(int id, string newPassword)
    {
        if (!IsAdmin) return RedirectToAction(nameof(Login));

        var user = await _db.Users.FindAsync(id);
        if (user != null && !string.IsNullOrWhiteSpace(newPassword) && newPassword.Length >= 8)
        {
            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            await _db.SaveChangesAsync();
            TempData["AdminMessage"] = $"Пароль аккаунта {user.PublicId} изменён.";
        }
        else
        {
            TempData["AdminMessage"] = "Новый пароль должен содержать минимум 8 символов.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearReservation(int id)
    {
        if (!IsAdmin) return RedirectToAction(nameof(Login));

        var slot = await _db.WashSlots.FindAsync(id);
        if (slot != null)
        {
            slot.ReservedBy = null;
            slot.ReservedByUserId = null;
            slot.ReservationWeekStart = null;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private static readonly string[] ScheduleDays =
        { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };

    private static readonly string[] ScheduleStatuses =
        { "Свободно", "Технические работы", "Занято", "Выходной", "Уборка", "Закрыто" };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSchedule(int id, string day, string time, string status)
    {
        if (!IsAdmin) return RedirectToAction(nameof(Login));

        var slot = await _db.WashSlots.FindAsync(id);
        if (slot == null) return RedirectToAction(nameof(Index));

        day = day?.Trim() ?? "";
        time = time?.Trim() ?? "";
        status = status?.Trim() ?? "";

        if (ScheduleDays.Contains(day) && time.Length > 0 && time.Length <= 30 &&
            ScheduleStatuses.Contains(status))
        {
            slot.Day = day;
            slot.Time = time;
            slot.Status = status;

            // A blocked/admin status cannot keep an old user reservation.
            if (status != "Свободно")
            {
                slot.ReservedBy = null;
                slot.ReservedByUserId = null;
                slot.ReservationWeekStart = null;
            }

            await _db.SaveChangesAsync();
            TempData["AdminMessage"] = $"Расписание изменено: {day}, {time}, статус «{status}».";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSchedule(string day, string time, string status)
    {
        if (!IsAdmin) return RedirectToAction(nameof(Login));

        day = day?.Trim() ?? "";
        time = time?.Trim() ?? "";
        status = status?.Trim() ?? "Свободно";

        if (ScheduleDays.Contains(day) && time.Length > 0 && time.Length <= 30 &&
            ScheduleStatuses.Contains(status))
        {
            _db.WashSlots.Add(new WashSlot
            {
                Day = day,
                Time = time,
                Status = status
            });
            await _db.SaveChangesAsync();
            TempData["AdminMessage"] = $"Добавлена стирка: {day}, {time}.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        if (!IsAdmin) return RedirectToAction(nameof(Login));

        var slot = await _db.WashSlots.FindAsync(id);
        if (slot != null)
        {
            _db.WashSlots.Remove(slot);
            await _db.SaveChangesAsync();
            TempData["AdminMessage"] = "Стирка удалена из расписания.";
        }

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove("Admin");
        return RedirectToAction(nameof(Login));
    }
}
