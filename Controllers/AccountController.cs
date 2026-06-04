using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ProflowApp.Data;
using ProflowApp.Models;
using ProFlowApp.Services;

namespace ProFlowApp.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _auditService;
    private readonly IMemoryCache _cache;
    private const int MAX_LOGIN_ATTEMPTS = 5;
    private const int LOCKOUT_MINUTES = 15;
    private const int RATE_LIMIT = 5;
    private const int RATE_WINDOW_MINUTES = 1;

    public AccountController(
        ApplicationDbContext context,
        AuditService auditService,
        IMemoryCache cache)
    {
        _context = context;
        _auditService = auditService;
        _cache = cache;
    }

    // Ambil IP dari header atau connection
    // Cek X-Forwarded-For dulu karena bisa jadi di balik proxy/tunnel
    private string GetClientIp()
    {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"]
                            .FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    // Cek apakah IP sudah melebihi batas percobaan
    private bool IsRateLimited(string ip)
    {
        var key = $"ratelimit_login_{ip}";
        if (_cache.TryGetValue(key, out int attempts))
            return attempts >= RATE_LIMIT;
        return false;
    }

    // Tambah counter percobaan untuk IP — otomatis expire setelah RATE_WINDOW_MINUTES
    private void IncrementRateLimit(string ip)
    {
        var key = $"ratelimit_login_{ip}";
        if (_cache.TryGetValue(key, out int attempts))
            _cache.Set(key, attempts + 1, TimeSpan.FromMinutes(RATE_WINDOW_MINUTES));
        else
            _cache.Set(key, 1, TimeSpan.FromMinutes(RATE_WINDOW_MINUTES));
    }

    public IActionResult Login()
    {
        // Tampilkan pesan blocked kalau dari redirect rate limiter
        if (Request.Query.ContainsKey("blocked"))
            ViewBag.Error = "Terlalu banyak percobaan login. Silakan tunggu 1 menit.";

        // Cegah browser cache halaman login
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        return View();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login(string username, string password)
    {
        var ip = GetClientIp();

        // Cek rate limit PERTAMA sebelum apapun
        // Mencegah brute force dari IP yang sama
        if (IsRateLimited(ip))
        {
            Console.WriteLine($"=== RATE LIMIT HIT: {ip} ===");

            ViewBag.Error = "Terlalu banyak percobaan login. Silakan tunggu 1 menit.";
            return View();
        }

        // Tambah counter setiap ada percobaan login
        IncrementRateLimit(ip);

        var key = $"ratelimit_login_{ip}";
        _cache.TryGetValue(key, out int currentAttempts);
        if (currentAttempts == RATE_LIMIT)
        {
            await _auditService.LogAsync(
                action: "RATE_LIMITED",
                entity: "User",
                detail: $"IP {ip} mencapai batas maksimal {RATE_LIMIT}x percobaan."
            );

            ViewBag.Error = "Terlalu Banyak percobaan login. Silahkan coba tunggu 1 menit.";
            return View();
        }

        // Cari user berdasarkan username
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted);

        // Jika user tidak ditemukan
        // Tambah delay agar response time sama dengan kasus password salah
        // Mencegah timing attack & user enumeration
        if (user == null)
        {
            await Task.Delay(300);

            await _auditService.LogAsync(
                action: "LOGIN_FAILED",
                entity: "User",
                detail: "Username atau password salah."
            );

            ViewBag.Error = "Username atau Password salah!";
            return View();
        }

        // Cek apakah akun sedang terkunci
        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.Now)
        {
            var sisaWaktu = (int)(user.LockedUntil.Value - DateTime.Now).TotalMinutes + 1;

            await _auditService.LogAsync(
                action: "LOGIN_BLOCKED",
                entity: "Users",
                entityId: user.UserID,
                detail: $"Akun terkunci. Sisa {sisaWaktu} menit.",
                overrideUsername: user.Username,
                overrideUserID: user.UserID
            );

            ViewBag.Error = $"Akun dikunci karena terlalu banyak percobaan gagal. " +
                           $"Coba lagi dalam {sisaWaktu} menit.";
            return View();
        }

        // Cek password dengan BCrypt
        if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            user.LoginAttempts++;

            // Kunci akun jika sudah mencapai batas maksimal
            if (user.LoginAttempts >= MAX_LOGIN_ATTEMPTS)
            {
                user.LockedUntil = DateTime.Now.AddMinutes(LOCKOUT_MINUTES);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    action: "ACCOUNT_LOCKED",
                    entity: "User",
                    entityId: user.UserID,
                    detail: $"Akun dikunci {LOCKOUT_MINUTES} menit setelah {MAX_LOGIN_ATTEMPTS}x gagal login.",
                    overrideUsername: user.Username,
                    overrideUserID: user.UserID
                );

                ViewBag.Error = $"Akun dikunci selama {LOCKOUT_MINUTES} menit.";
                return View();
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "LOGIN_FAILED",
                entity: "User",
                entityId: user.UserID,
                detail: $"Password salah. Percobaan ke-{user.LoginAttempts}.",
                overrideUsername: user.Username,
                overrideUserID: user.UserID
            );

            var sisaPercobaan = MAX_LOGIN_ATTEMPTS - user.LoginAttempts;
            ViewBag.Error = $"Username atau Password salah! Sisa percobaan: {sisaPercobaan}x";
            return View();
        }

        // Login BERHASIL — reset semua counter
        user.LoginAttempts = 0;
        user.LockedUntil = null;
        await _context.SaveChangesAsync();

        HttpContext.Session.SetString("Username", user.Username);
        HttpContext.Session.SetString("Role", user.Role);
        HttpContext.Session.SetString("UserID", user.UserID);

        await _auditService.LogAsync(
            action: "LOGIN",
            entity: "Users",
            entityId: user.UserID,
            detail: $"Login berhasil. Role: {user.Role}"
        );

        return user.Role switch
        {
            "Checker" => RedirectToAction("Index", "Pengajuan"),
            _ => RedirectToAction("Index", "Barang")
        };
    }

    public async Task<IActionResult> Logout()
    {
        var userID = HttpContext.Session.GetString("UserID");

        await _auditService.LogAsync(
            action: "LOGOUT",
            entity: "Users",
            entityId: userID,
            detail: "User logout."
        );

        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        var username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(model.PasswordLama, user.Password))
        {
            ModelState.AddModelError("PasswordLama", "Password lama yang Anda masukkan salah.");
            return View(model);
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(model.PasswordBaru);
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "CHANGE_PASSWORD",
            entity: "Users",
            entityId: user.UserID,
            detail: "Password berhasil diubah"
        );

        TempData["Success"] = "Password berhasil diperbarui!";
        return RedirectToAction("Index", "Home");
    }
}