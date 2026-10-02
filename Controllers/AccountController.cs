using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LaundrySite.Data;
using LaundrySite.Models;

namespace LaundrySite.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();

    public AccountController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string firstName, string lastName, string roomNumber, string password, string passwordConfirm)
    {
        firstName = firstName?.Trim() ?? "";
        lastName = lastName?.Trim() ?? "";
        roomNumber = roomNumber?.Trim() ?? "";
        password ??= "";
        passwordConfirm ??= "";

        if (firstName.Length == 0 || lastName.Length == 0 || roomNumber.Length == 0 || password.Length < 8)
            ModelState.AddModelError("", "Заполните все поля. Пароль должен содержать минимум 8 символов.");

        if (firstName.Length > 30 || lastName.Length > 30)
            ModelState.AddModelError("", "Имя и фамилия должны содержать не более 30 символов.");

        if (!IsLettersOnly(firstName))
            ModelState.AddModelError("", "Имя может содержать только буквы.");

        if (!IsLettersOnly(lastName))
            ModelState.AddModelError("", "Фамилия может содержать только буквы.");

        if (!int.TryParse(roomNumber, out var room) || room < 1 || room > 1000 || roomNumber != room.ToString())
            ModelState.AddModelError("", "Номер комнаты должен быть целым положительным числом от 1 до 1000.");

        if (password != passwordConfirm)
            ModelState.AddModelError("", "Пароли не совпадают.");

        var existing = await _db.Users.FirstOrDefaultAsync(u =>
            u.FirstName.ToLower() == firstName.ToLower() &&
            u.LastName.ToLower() == lastName.ToLower() &&
            u.RoomNumber.ToLower() == roomNumber.ToLower());

        if (!ModelState.IsValid) return View();

        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(existing.PasswordHash))
            {
                ModelState.AddModelError("", "Такой аккаунт уже существует. Войдите по ID аккаунта.");
                return View();
            }

            existing.PublicId = string.IsNullOrWhiteSpace(existing.PublicId) ? CreatePublicId() : existing.PublicId;
            existing.WeeklyBookingLimit = existing.WeeklyBookingLimit <= 0 ? 2 : existing.WeeklyBookingLimit;
            existing.PasswordHash = _hasher.HashPassword(existing, password);
            await _db.SaveChangesAsync();
            SignIn(existing);
            TempData["AccountId"] = existing.PublicId;
            return RedirectToAction("Index", "Home");
        }

        var user = new User
        {
            PublicId = CreatePublicId(),
            FirstName = firstName,
            LastName = lastName,
            RoomNumber = roomNumber,
            WeeklyBookingLimit = 2
        };
        user.PasswordHash = _hasher.HashPassword(user, password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        SignIn(user);
        TempData["AccountId"] = user.PublicId;
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string publicId, string password)
    {
        publicId = publicId?.Trim() ?? "";

        if (publicId.Length == 0 || password.Length == 0)
        {
            ModelState.AddModelError("", "Введите ID аккаунта и пароль.");
            return View();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == publicId);

        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            ModelState.AddModelError("", "Неверный ID или пароль.");
            return View();
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Неверный ID или пароль.");
            return View();
        }

        // Upgrade the hash if the framework recommends a stronger format.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await _db.SaveChangesAsync();
        }

        SignIn(user);
        return RedirectToAction("Index", "Home");
    }

    private static bool IsLettersOnly(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(char.IsLetter);

    private static string CreatePublicId() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private void SignIn(User user)
    {
        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("UserPublicId", user.PublicId);
        HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString("Room", user.RoomNumber);
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
