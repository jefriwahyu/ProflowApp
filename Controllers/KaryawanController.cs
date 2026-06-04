using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProflowApp.Data;
using ProflowApp.Models;
using ProFlowApp.Services;

namespace ProFlowApp.Controllers
{
    public class KaryawanController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        // Inject AuditService — sama seperti controller lain
        // Prinsip KISS: satu service untuk semua logging
        public KaryawanController(
            ApplicationDbContext context,
            AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // Index & Trash tidak perlu log — hanya read data
        public async Task<IActionResult> Index(int? page, string? search = null)
        {
            int pageSize = 5;
            int pageNumber = page ?? 1;
            if (pageNumber < 1) pageNumber = 1;

            var query = _context.Users
                .Where(u => !u.IsDeleted && u.Role == "Karyawan")

                // 1 search box — cari di UserID, Nama, dan Username sekaligus
                // Kosong = tampilkan semua
                .Where(u => string.IsNullOrEmpty(search) ||
                            u.UserID.Contains(search) ||
                            u.Nama.Contains(search) ||
                            u.Username.Contains(search))

                .OrderBy(u => u.Nama);

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.FilterSearch = search;

            return View(items);
        }

        public IActionResult Create() => View();

        private string GenerateRandomUserID()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User newUser)
        {
            bool isUsernameTaken = await _context.Users
                .AnyAsync(u => u.Username == newUser.Username);

            if (isUsernameTaken)
                ModelState.AddModelError("Username",
                    "Username ini sudah digunakan. Silakan cari yang lain.");

            newUser.Role = "Karyawan";
            newUser.IsDeleted = false;

            if (ModelState.IsValid)
            {
                string newID;
                bool isExist;

                do
                {
                    newID = GenerateRandomUserID();
                    isExist = await _context.Users.AnyAsync(u => u.UserID == newID);
                } while (isExist);

                newUser.UserID = newID;
                newUser.Password = BCrypt.Net.BCrypt.HashPassword(newUser.Password);

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                // Log setelah SaveChanges agar UserID sudah ter-generate
                // Detail: Nama + Username agar mudah diidentifikasi
                // tanpa perlu query ke tabel Users lagi
                await _auditService.LogAsync(
                    action: "CREATE_USER",
                    entity: "Users",
                    entityId: newUser.UserID,
                    detail: $"Karyawan baru ditambahkan: {newUser.Nama} | " +
                              $"Username: {newUser.Username}"
                );

                TempData["Success"] = $"Akun {newUser.Nama} berhasil dibuat.";
                return RedirectToAction(nameof(Index));
            }

            return View(newUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (HttpContext.Session.GetString("Role") != "Manager")
                return RedirectToAction("Index");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var currentUsername = HttpContext.Session.GetString("Username");
            if (user.Username == currentUsername)
            {
                TempData["Error"] = "Anda tidak bisa menghapus akun sendiri.";
                return RedirectToAction(nameof(Index));
            }

            user.IsDeleted = true;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Log DELETE — aksi sensitif, catat nama + username
            // yang dinonaktifkan agar mudah dilacak
            await _auditService.LogAsync(
                action: "DELETE_USER",
                entity: "Users",
                entityId: user.UserID,
                detail: $"Akun dinonaktifkan: {user.Nama} | " +
                          $"Username: {user.Username}"
            );

            TempData["Success"] = $"Akun {user.Nama} berhasil dinonaktifkan.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            if (HttpContext.Session.GetString("Role") != "Manager")
                return RedirectToAction("Index");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsDeleted = false;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Log RESTORE — catat nama + username yang diaktifkan kembali
            await _auditService.LogAsync(
                action: "RESTORE_USER",
                entity: "Users",
                entityId: user.UserID,
                detail: $"Akun diaktifkan kembali: {user.Nama} | " +
                          $"Username: {user.Username}"
            );

            TempData["Success"] = $"Akun {user.Nama} telah diaktifkan kembali.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Trash(int? page, string? search = null)
        {
            int pageSize = 5;
            int pageNumber = page ?? 1;

            var query = _context.Users
                .Where(u => u.IsDeleted)
                .Where(u => string.IsNullOrEmpty(search) ||
                    u.UserID.Contains(search) ||
                    u.Nama.Contains(search) ||
                    u.Username.Contains(search))
                .OrderByDescending(u => u.Nama);

            int totalItems = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.TotalItems = totalItems;

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, User user)
        {
            if (id != user.UserID) return BadRequest();

            bool isUsernameTaken = await _context.Users
                .AnyAsync(u => u.Username == user.Username && u.UserID != id);

            if (isUsernameTaken)
                ModelState.AddModelError("Username",
                    "Username ini sudah digunakan. Silakan cari yang lain.");

            // Ambil data lama SEBELUM update untuk dicatat di log
            // Sehingga ada jejak "dari X menjadi Y"
            var existingUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserID == id);

            if (existingUser == null) return NotFound();

            user.Role = existingUser.Role;
            user.IsDeleted = existingUser.IsDeleted;

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                user.Password = existingUser.Password;
                ModelState.Remove("Password");
            }
            else
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    // Log EDIT — catat perubahan nama dan username
                    // Format "lama → baru" agar mudah dibaca
                    // Tidak log perubahan password karena sensitif
                    await _auditService.LogAsync(
                        action: "EDIT_USER",
                        entity: "Users",
                        entityId: user.UserID,
                        detail: $"Data karyawan diedit: " +
                                  $"Nama: {existingUser.Nama} → {user.Nama} | " +
                                  $"Username: {existingUser.Username} → {user.Username}"
                    );

                    TempData["Success"] = "Data karyawan berhasil diperbarui.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Users.Any(u => u.UserID == id)) return NotFound();
                    throw;
                }
            }

            return View(user);
        }
    }
}