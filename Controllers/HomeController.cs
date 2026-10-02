using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using LaundrySite.Data;
using LaundrySite.Models;

namespace LaundrySite.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    private static DateTime WeekStart(DateTime date)
    {
        var d = date.Date;
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }

    private static string ReservationText(User user) => $"{user.FirstName} {user.LastName} (комната {user.RoomNumber})";

    public async Task<IActionResult> Index()
    {
        var slots = await _db.WashSlots.OrderBy(x => x.Id).ToListAsync();
        var week = WeekStart(DateTime.Now);

        foreach (var slot in slots)
        {
            if (slot.ReservationWeekStart?.Date != week.Date)
            {
                slot.ReservedBy = null;
                slot.ReservedByUserId = null;
            }
        }

        var userId = HttpContext.Session.GetInt32("UserId");
        User? user = userId.HasValue ? await _db.Users.FindAsync(userId.Value) : null;

        ViewBag.UserName = user == null ? null : $"{user.FirstName} {user.LastName}";
        ViewBag.Room = user?.RoomNumber;
        ViewBag.UserPublicId = user?.PublicId;
        ViewBag.WeeklyBookings = user == null ? 0 : await CurrentUserBookingCount(user.Id, week);
        ViewBag.WeeklyLimit = user?.WeeklyBookingLimit ?? 2;
        ViewBag.WeekStart = week;

        if (TempData["AccountId"] != null)
            ViewBag.NewAccountId = TempData["AccountId"]?.ToString();

        return View(slots);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reserve(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
            return RedirectToAction("Login", "Account");

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        var week = WeekStart(DateTime.Now);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var currentBookings = await CurrentUserBookingCount(user.Id, week);
        var limit = Math.Max(0, user.WeeklyBookingLimit);

        if (currentBookings >= limit)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = $"Ваш недельный лимит исчерпан: {limit} стирки(ок).";
            return RedirectToAction(nameof(Index));
        }

        var slot = await _db.WashSlots.FindAsync(id);
        if (slot == null)
        {
            await transaction.RollbackAsync();
            return RedirectToAction(nameof(Index));
        }

        if (slot.ReservationWeekStart?.Date != week.Date)
        {
            slot.ReservedBy = null;
            slot.ReservedByUserId = null;
            slot.ReservationWeekStart = null;
        }

        if (slot.Status != "Свободно")
        {
            await transaction.RollbackAsync();
            TempData["Error"] = $"Эта стирка недоступна: статус «{slot.Status}».";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(slot.ReservedBy) && slot.ReservedByUserId == null)
        {
            slot.ReservedBy = ReservationText(user);
            slot.ReservedByUserId = user.Id;
            slot.ReservationWeekStart = week;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        else
        {
            await transaction.RollbackAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var week = WeekStart(DateTime.Now);
        var slot = await _db.WashSlots.FindAsync(id);

        if (slot != null &&
            slot.ReservationWeekStart?.Date == week.Date &&
            slot.ReservedByUserId == userId.Value)
        {
            slot.ReservedBy = null;
            slot.ReservedByUserId = null;
            slot.ReservationWeekStart = null;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private Task<int> CurrentUserBookingCount(int userId, DateTime week) =>
        _db.WashSlots.CountAsync(s =>
            s.ReservationWeekStart != null &&
            s.ReservationWeekStart.Value.Date == week.Date &&
            s.ReservedByUserId == userId);
}
