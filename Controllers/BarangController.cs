using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProflowApp.Data;
using ProflowApp.Models;
using ProFlowApp.Services;

namespace ProFlowApp.Controllers;

public class BarangController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _auditService;

    public BarangController(ApplicationDbContext context, AuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public IActionResult Create()
    {
        if (HttpContext.Session.GetString("Role") != "Manager")
            return RedirectToAction("Index");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Barang barang)
    {
        // Validasi null/empty sebelum check duplikasi
        if (string.IsNullOrWhiteSpace(barang.Nm_Brg))
        {
            ModelState.AddModelError("Nm_Brg", "Nama barang tidak boleh kosong.");
        }
        else
        {
            bool isBrgNameTaken = await _context.Barang
                .AnyAsync(b => b.Nm_Brg.ToLower() == barang.Nm_Brg.ToLower());

            if (isBrgNameTaken)
                ModelState.AddModelError("Nm_Brg",
                    "Nama barang ini sudah terdaftar di sistem (mungkin ada di Tempat Sampah).");
        }

        if (HttpContext.Session.GetString("Role") != "Manager")
            return RedirectToAction("Index");

        if (ModelState.IsValid)
        {
            _context.Barang.Add(barang);
            await _context.SaveChangesAsync();

            // Log setelah SaveChanges agar Brg_ID sudah ter-generate
            // Detail mencatat nama, satuan, harga agar informatif
            // tanpa perlu JOIN ke tabel Barang saat baca log
            await _auditService.LogAsync(
                action: "CREATE_BARANG",
                entity: "Barang",
                entityId: barang.Brg_ID.ToString(),
                detail: $"Barang ditambahkan: {barang.Nm_Brg} | " +
                          $"Satuan: {barang.Satuan} | " +
                          $"Harga Est: {barang.Hrg_Est}" +
                          $"Vendor: {barang.Nm_Vendor}"
            );

            TempData["Success"] = "Barang berhasil ditambahkan.";
            return RedirectToAction(nameof(Index));
        }

        return View(barang);
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        if (HttpContext.Session.GetString("Role") != "Manager")
            return RedirectToAction("Index");

        var barang = await _context.Barang.FindAsync(id);
        if (barang == null) return NotFound();
        return View(barang);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Barang barang)
    {
        // Ambil data lama SEBELUM update untuk dicatat di log
        // Tujuan: ada jejak "dari X menjadi Y"
        // AsNoTracking agar tidak konflik dengan _context.Update nanti
        var existingBarang = await _context.Barang.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Brg_ID == id);

        // Validasi null/empty sebelum check duplikasi
        if (string.IsNullOrWhiteSpace(barang.Nm_Brg))
        {
            ModelState.AddModelError("Nm_Brg", "Nama barang tidak boleh kosong.");
        }
        else
        {
            bool isBrgNameTaken = await _context.Barang
                .AnyAsync(b => b.Nm_Brg.ToLower() == barang.Nm_Brg.ToLower()
                            && b.Brg_ID != id); // exclude diri sendiri

            if (isBrgNameTaken)
                ModelState.AddModelError("Nm_Brg",
                    "Nama barang ini sudah terdaftar di sistem (mungkin ada di Tempat Sampah).");
        }

        if (id != barang.Brg_ID) return BadRequest();

        if (ModelState.IsValid)
        {
            _context.Update(barang);
            await _context.SaveChangesAsync();

            // Catat perubahan nama dan harga — format "lama → baru"
            // Tidak log IsDeleted karena ada action Delete/Restore tersendiri
            await _auditService.LogAsync(
                action: "EDIT_BARANG",
                entity: "Barang",
                entityId: barang.Brg_ID.ToString(),
                detail: $"Barang diedit: " +
                          $"Nama: {existingBarang?.Nm_Brg} → {barang.Nm_Brg} | " +
                          $"Harga: {existingBarang?.Hrg_Est} → {barang.Hrg_Est}" +
                          $"Vendor: {existingBarang?.Nm_Vendor} → {barang.Nm_Vendor}"
            );

            TempData["Success"] = "Barang berhasil diperbarui.";
            return RedirectToAction(nameof(Index));
        }

        return View(barang);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var barang = await _context.Barang.FindAsync(id);
        if (barang == null) return NotFound();

        barang.IsDeleted = true;
        _context.Barang.Update(barang);
        await _context.SaveChangesAsync();

        // Log soft delete — catat nama barang yang dihapus
        await _auditService.LogAsync(
            action: "DELETE_BARANG",
            entity: "Barang",
            entityId: barang.Brg_ID.ToString(),
            detail: $"Barang dihapus dari katalog: {barang.Nm_Brg}"
        );

        TempData["Success"] = "Barang berhasil dihapus dari katalog.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        if (HttpContext.Session.GetString("Role") != "Manager")
            return RedirectToAction("Index");

        var barang = await _context.Barang.FindAsync(id);
        if (barang == null) return NotFound();

        barang.IsDeleted = false;
        _context.Barang.Update(barang);
        await _context.SaveChangesAsync();

        // Log restore — catat nama barang yang dikembalikan
        await _auditService.LogAsync(
            action: "RESTORE_BARANG",
            entity: "Barang",
            entityId: barang.Brg_ID.ToString(),
            detail: $"Barang dikembalikan ke katalog: {barang.Nm_Brg}"
        );

        TempData["Success"] = "Barang berhasil dikembalikan ke katalog.";
        return RedirectToAction(nameof(Trash));
    }

    // Index & Trash tidak perlu log — hanya read data
    public async Task<IActionResult> Index(int page = 1, string? search = null)
    {
        if (page < 1) page = 1;

        var query = _context.Barang
            .Where(b => !b.IsDeleted)
            .Where(b => string.IsNullOrEmpty(search) ||
                        b.Nm_Brg.Contains(search))
            .OrderBy(b => b.Brg_ID);

        int pageSize = 5;
        int totalData = await query.CountAsync();
        int totalPages = (int)Math.Ceiling((double)totalData / pageSize);

        if (page > totalPages && totalPages > 0) page = totalPages;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.FilterSearch = search;

        return View(items);
    }

    public async Task<IActionResult> Trash(int? page, string? search = null)
    {
        int pageSize = 5;
        int pageNumber = page ?? 1;
        if (pageNumber < 1) pageNumber = 1;

        var query = _context.Barang
            .Where(b => b.IsDeleted)
            .Where(b => string.IsNullOrEmpty(search) ||
                        b.Nm_Brg.Contains(search) ||
                        b.Nm_Vendor.Contains(search))
            .OrderByDescending(b => b.Nm_Brg);

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
}