using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProflowApp.Data;
using ProflowApp.Models;
using ProFlowApp.Services;
using ProFlowApp.ViewModels;

namespace ProFlowApp.Controllers;

[Route("Pengajuan")]
public class PengajuanController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _auditService;
    private readonly IWebHostEnvironment _env;

    // IWebHostEnvironment — untuk mendapatkan path wwwroot
    // agar bisa simpan foto ke folder uploads/bukti
    public PengajuanController(
        ApplicationDbContext context,
        AuditService auditService,
        IWebHostEnvironment env)
    {
        _context = context;
        _auditService = auditService;
        _env = env;
    }

    // ==================== CREATE ====================

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        // Hanya tampilkan barang yang tidak dihapus
        ViewBag.BarangList = new SelectList(
            await _context.Barang.Where(b => !b.IsDeleted).ToListAsync(),
            "Brg_ID", "Nm_Brg");
        return View();
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(Pengajuan pr)
    {
        var userID = HttpContext.Session.GetString("UserID");
        if (userID != "") return RedirectToAction("Login", "Account");

        pr.NoPR = "PR" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        pr.UserID = userID;
        pr.Tgl_Req = DateTime.Now;
        pr.Status = 0;

        ModelState.Remove("NoPR");
        ModelState.Remove("UserID");

        if (ModelState.IsValid)
        {
            var barang = await _context.Barang.FindAsync(pr.Brg_ID);

            _context.Pengajuan.Add(pr);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "CREATE_PR",
                entity: "Pengajuan",
                entityId: pr.PR_ID.ToString(),
                detail: $"PR dibuat: {pr.NoPR} | Barang: {barang?.Nm_Brg} | Jumlah: {pr.Jml}"
            );

            return RedirectToAction("Index", "Pengajuan");
        }

        ViewBag.BarangList = new SelectList(
            await _context.Barang.Where(b => !b.IsDeleted).ToListAsync(),
            "Brg_ID", "Nm_Brg", pr.Brg_ID);
        return View(pr);
    }

    // ==================== MANAGER ACTIONS ====================

    // Manager assign PR ke checker — status 0 → 1
    // Tidak perlu pilih checker karena hanya ada 1 checker
    [HttpPost("AssignChecker")]
    public async Task<IActionResult> AssignChecker(string noPR)
    {
        if (HttpContext.Session.GetString("Role") != "Manager")
        {
            TempData["Error"] = "Anda tidak memiliki akses.";
            return RedirectToAction("Index");
        }

        var pr = await _context.Pengajuan
            .FirstOrDefaultAsync(p => p.NoPR == noPR);

        // Hanya bisa assign kalau status masih Pending
        if (pr == null || pr.Status != 0)
        {
            TempData["Error"] = "PR tidak ditemukan atau status tidak valid.";
            return RedirectToAction("Index");
        }

        pr.Status = 1; // Perlu Dicek
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "ASSIGN_CHECKER",
            entity: "Pengajuan",
            entityId: pr.PR_ID.ToString(),
            detail: $"PR dikirim ke checker: {noPR}"
        );

        TempData["Success"] = $"PR {noPR} berhasil dikirim ke checker.";
        return RedirectToAction("Index");
    }

    // Manager approve PR — status 2 → 3
    // Sekaligus simpan feedback dan buat PO
    [HttpPost("Approve")]
    public async Task<IActionResult> Approve(string noPR, string feedback)
    {
        if (HttpContext.Session.GetString("Role") != "Manager")
        {
            TempData["Error"] = "Anda tidak memiliki akses.";
            return RedirectToAction("Index");
        }

        var pr = await _context.Pengajuan
            .FirstOrDefaultAsync(p => p.NoPR == noPR);

        // Hanya bisa approve kalau status Sudah Dicek
        // Mencegah manager approve sebelum checker selesai
        if (pr == null || pr.Status != 2)
        {
            TempData["Error"] = "PR tidak ditemukan atau belum dicek oleh checker.";
            return RedirectToAction("Index");
        }

        // Feedback wajib diisi
        if (string.IsNullOrWhiteSpace(feedback))
        {
            TempData["Error"] = "Feedback wajib diisi sebelum menyetujui.";
            return RedirectToAction("Index");
        }

        pr.Status = 3; // Disetujui
        pr.Feedback = feedback;
        pr.TglFeedback = DateTime.Now;

        // Buat PO otomatis
        var noPO = "PO" + DateTime.Now.ToString("yyyyMMdd") + "-" + pr.PR_ID;
        var pesananBaru = new Pesanan
        {
            NoPO = noPO,
            PR_ID = pr.PR_ID,
            tgl_PO = DateTime.Now
        };

        _context.Pesanan.Add(pesananBaru);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "APPROVE_PR",
            entity: "Pengajuan",
            entityId: pr.PR_ID.ToString(),
            detail: $"PR disetujui: {noPR} | PO dibuat: {noPO} | Feedback: {feedback}"
        );

        TempData["Success"] = $"PR {noPR} berhasil disetujui.";
        return RedirectToAction("Index");
    }

    // Manager reject PR — bisa dari status 0 atau 2
    // Status 0 = langsung tolak tanpa checker
    // Status 2 = tolak setelah checker cek
    [HttpPost("Reject")]
    public async Task<IActionResult> Reject(string noPR, string feedback)
    {
        if (HttpContext.Session.GetString("Role") != "Manager")
        {
            TempData["Error"] = "Anda tidak memiliki akses.";
            return RedirectToAction("Index");
        }

        var pr = await _context.Pengajuan
            .FirstOrDefaultAsync(p => p.NoPR == noPR);

        // Bisa reject dari status Pending (0) atau Sudah Dicek (2)
        if (pr == null || (pr.Status != 0 && pr.Status != 2))
        {
            TempData["Error"] = "PR tidak ditemukan atau status tidak valid.";
            return RedirectToAction("Index");
        }

        // Feedback wajib diisi
        if (string.IsNullOrWhiteSpace(feedback))
        {
            TempData["Error"] = "Feedback wajib diisi sebelum menolak.";
            return RedirectToAction("Index");
        }

        pr.Status = 4; // Ditolak
        pr.Feedback = feedback;
        pr.TglFeedback = DateTime.Now;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "REJECT_PR",
            entity: "Pengajuan",
            entityId: pr.PR_ID.ToString(),
            detail: $"PR ditolak: {noPR} | Feedback: {feedback}"
        );

        TempData["Success"] = $"PR {noPR} berhasil ditolak.";
        return RedirectToAction("Index");
    }

    // ==================== CHECKER ACTIONS ====================

    // Checker upload foto bukti + keterangan
    // Dipanggil dari modal sebelum klik "Sudah Dicek"
    [HttpPost("KirimBukti")]
    public async Task<IActionResult> KirimBukti(
    string noPR,
    IFormFile fotoBukti,
    string ketChecker)
    {
        if (HttpContext.Session.GetString("Role") != "Checker")
        {
            TempData["Error"] = "Anda tidak memiliki akses.";
            return RedirectToAction("Index");
        }

        var pr = await _context.Pengajuan
            .FirstOrDefaultAsync(p => p.NoPR == noPR);

        if (pr == null || pr.Status != 1)
        {
            TempData["Error"] = "PR tidak ditemukan atau status tidak valid.";
            return RedirectToAction("Index");
        }

        if (fotoBukti == null || fotoBukti.Length == 0)
        {
            TempData["Error"] = "Foto bukti wajib diupload.";
            return RedirectToAction("Index");
        }

        if (string.IsNullOrWhiteSpace(ketChecker))
        {
            TempData["Error"] = "Keterangan wajib diisi.";
            return RedirectToAction("Index");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(fotoBukti.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
        {
            TempData["Error"] = "File harus berformat JPG atau PNG.";
            return RedirectToAction("Index");
        }

        if (fotoBukti.Length > 5 * 1024 * 1024)
        {
            TempData["Error"] = "Ukuran file maksimal 5MB.";
            return RedirectToAction("Index");
        }

        var safeNoPR = string.Concat(noPR.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{safeNoPR}{extension}";
        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "bukti");
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await fotoBukti.CopyToAsync(stream);

        pr.FotoBukti = $"/uploads/bukti/{fileName}";
        pr.KetChecker = ketChecker;
        pr.TglChecker = DateTime.Now;

        // Langsung ubah status ke 2 (Sudah Dicek) setelah upload
        pr.Status = 2;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "KIRIM_BUKTI",
            entity: "Pengajuan",
            entityId: pr.PR_ID.ToString(),
            detail: $"Bukti dikirim & status diubah ke Sudah Dicek: {noPR} | File: {fileName}"
        );

        TempData["Success"] = "Bukti berhasil dikirim. Status pengajuan diubah ke Sudah Dicek.";
        return RedirectToAction("Index");
    }

    // ==================== INDEX ====================

    public async Task<IActionResult> Index(
    int page = 1,
    string? search = null,
    string? tanggalDari = null,
    string? tanggalSampai = null,
    int? status = null)
    {
        // Pastikan page selalu minimal 1 — mencegah OFFSET negatif
        if (page < 1) page = 1;

        int pageSize = 5;
        var role = HttpContext.Session.GetString("Role");
        var userID = HttpContext.Session.GetString("UserID");

        // TryParse lebih aman dari Parse — tidak throw exception kalau format salah
        DateTime? tglDari = DateTime.TryParse(tanggalDari, out var td) ? td : null;
        DateTime? tglSampai = DateTime.TryParse(tanggalSampai, out var ts) ? ts : null;

        var query = from pr in _context.Pengajuan
                    where role == "Manager" ||
                          (role == "Checker" && pr.Status == 1) ||
                          pr.UserID == userID

                    join brg in _context.Barang on pr.Brg_ID equals brg.Brg_ID
                    join usr in _context.Users on pr.UserID equals usr.UserID
                    join psn in _context.Pesanan on pr.PR_ID equals psn.PR_ID into psnGroup
                    from psn in psnGroup.DefaultIfEmpty()

                    where string.IsNullOrEmpty(search) ||
                          pr.NoPR.Contains(search) ||
                          (psn != null && psn.NoPO != null && psn.NoPO.Contains(search)) ||
                          usr.Nama.Contains(search)

                    where tglDari == null || pr.Tgl_Req >= tglDari
                    where tglSampai == null || pr.Tgl_Req <= tglSampai.Value.Date.AddDays(1).AddSeconds(-1)
                    where status == null || pr.Status == status

                    select new PengajuanIndexViewModel
                    {
                        NoPR = pr.NoPR,
                        NamaBarang = brg.Nm_Brg,
                        Jumlah = pr.Jml,
                        Status = pr.Status,
                        Tanggal = pr.Tgl_Req,
                        NamaKaryawan = usr.Nama,
                        TglPR = pr.Tgl_Req,
                        Keterangan = pr.Keterangan,
                        NoPO = psn != null ? psn.NoPO : "-",
                        FotoBukti = pr.FotoBukti,
                        KetChecker = pr.KetChecker,
                        TglChecker = pr.TglChecker,
                        Feedback = pr.Feedback,
                        TglFeedback = pr.TglFeedback
                    };

        var totalData = await query.CountAsync();
        int totalPages = (int)Math.Ceiling((double)totalData / pageSize);

        // Pastikan page tidak melebihi totalPages
        if (page > totalPages && totalPages > 0) page = totalPages;

        var dataPaginated = await query
            .OrderByDescending(x => x.Tanggal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.Role = role;
        ViewBag.FilterSearch = search;
        ViewBag.FilterTanggalDari = tanggalDari;
        ViewBag.FilterTanggalSampai = tanggalSampai;
        ViewBag.FilterStatus = status;

        return View(dataPaginated);
    }
}