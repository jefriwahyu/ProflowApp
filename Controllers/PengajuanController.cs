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
    private readonly ClassifierClient _classifierClient;

    // IWebHostEnvironment — untuk mendapatkan path wwwroot
    // agar bisa simpan foto ke folder uploads/bukti
    public PengajuanController(
        ApplicationDbContext context,
        AuditService auditService,
        IWebHostEnvironment env,
        ClassifierClient classifierClient)
    {
        _context = context;
        _auditService = auditService;
        _env = env;
        _classifierClient = classifierClient;
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
        if (userID == null) return RedirectToAction("Login", "Account");

        pr.NoPR = "PR" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        pr.UserID = userID;
        pr.Tgl_Req = DateTime.Now;
        pr.Status = 0;

        ModelState.Remove("NoPR");
        ModelState.Remove("UserID");

        if (ModelState.IsValid)
        {
            var barang = await _context.Barang.FindAsync(pr.Brg_ID);

            // ---- klasifikasi urgensi: Kategori barang + Keterangan pengajuan ----
            try
            {
                pr.UrgencyLevel = await _classifierClient.ClassifyAsync(
                    barang?.Kategori ?? "",
                    pr.Keterangan ?? ""
                );
            }
            catch (Exception ex)
            {
                pr.UrgencyLevel = "Medium"; // fallback kalau microservice down
            }
            // ----------------------------------------------------------------------

            _context.Pengajuan.Add(pr);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "CREATE_PR",
                entity: "Pengajuan",
                entityId: pr.PR_ID.ToString(),
                detail: $"PR dibuat: {pr.NoPR} | Barang: {barang?.Nm_Brg} | Jumlah: {pr.Jml} | Urgensi: {pr.UrgencyLevel}"
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
    public async Task<IActionResult> Approve(string noPR, string feedback, string decisionType)
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

        if (pr.Rekomendasi != "SERVICE" && pr.Rekomendasi != "GANTI_BARU")
        {
            TempData["Error"] = "Rekomendasi checker tidak valid atau belum diisi.";
            return RedirectToAction("Index");
        }

        // Harga service sudah diinput checker saat kirim bukti.
        // Manager tidak lagi input harga di sini.
        if (pr.Rekomendasi == "SERVICE" && (pr.HargaService == null || pr.HargaService <= 0))
        {
            TempData["Error"] = "Harga Service belum diisi oleh checker. Minta checker melengkapi terlebih dahulu.";
            return RedirectToAction("Index");
        }

        var barang = await _context.Barang.FindAsync(pr.Brg_ID);

        pr.Status = 3; // Disetujui
        pr.Feedback = feedback;
        pr.TglFeedback = DateTime.Now;
        pr.DecisionType = pr.Rekomendasi == "SERVICE" ? "SERVICE" : "PENGADAAN";

        decimal totalHarga = pr.Rekomendasi == "SERVICE"
            ? pr.HargaService!.Value
            : (barang?.Hrg_Est ?? 0) * pr.Jml;

        // Buat PO otomatis 
        var lastNoPO = await _context.Pesanan
            .Where(p => p.NoPO != null && p.NoPO.StartsWith("TXN"))
            .OrderByDescending(p => p.PO_ID)
            .Select(p => p.NoPO)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (!string.IsNullOrEmpty(lastNoPO) && lastNoPO.StartsWith("TXN"))
        {
            var numericPart = lastNoPO.Substring(3); // buang "TXN", ambil angkanya
            if (int.TryParse(numericPart, out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        var noPO = "TXN" + nextNumber.ToString("D3"); // D3 = padding 3 digit: 1 -> "001"

        var pesananBaru = new Pesanan
        {
            NoPO = noPO,
            PR_ID = pr.PR_ID,
            tgl_PO = DateTime.Now,
            Status = 3,
            // TotalHarga = barang?.Hrg_Est * pr.Jml,
            TotalHarga = totalHarga
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
    string ketChecker,
    string rekomendasi,
    decimal? hargaService)
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

        if (rekomendasi != "SERVICE" && rekomendasi != "GANTI_BARU")
        {
            TempData["Error"] = "Rekomendasi wajib dipilih (Service atau Ganti Baru).";
            return RedirectToAction("Index");
        }

        // Harga service wajib diisi checker jika rekomendasi = Service
        if (rekomendasi == "SERVICE" && (hargaService == null || hargaService <= 0))
        {
            TempData["Error"] = "Harga Service wajib diisi untuk rekomendasi Service.";
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
        pr.Rekomendasi = rekomendasi;

        // Simpan harga service (dari checker) jika rekomendasi Service.
        // Untuk Ganti Baru, harga diambil dari estimasi barang saat approve.
        pr.HargaService = rekomendasi == "SERVICE" ? hargaService : null;

        // Langsung ubah status ke 2 (Sudah Dicek) setelah upload
        pr.Status = 2;

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "KIRIM_BUKTI",
            entity: "Pengajuan",
            entityId: pr.PR_ID.ToString(),
            detail: $"Bukti dikirim & status diubah ke Sudah Dicek: {noPR} | File: {fileName} | Rekomendasi: {rekomendasi}"
                + (rekomendasi == "SERVICE" ? $" | Harga Service: {hargaService}" : "")
        );

        TempData["Success"] = "Bukti berhasil dikirim. Status pengajuan diubah ke Sudah Dicek.";
        return RedirectToAction("Index");
    }

    // Checker upload bukti barang sudah diterima / sudah diservice.
    // Dipanggil setelah PR disetujui manager (status 3).
    // - DecisionType PENGADAAN (Ganti Baru) → status 5 (Diterima)
    // - DecisionType SERVICE                → status 6 (Diservice)
    [HttpPost("KirimBuktiTerima")]
    public async Task<IActionResult> KirimBuktiTerima(string noPR, IFormFile fotoTerima)
    {
        if (HttpContext.Session.GetString("Role") != "Checker")
        {
            TempData["Error"] = "Anda tidak memiliki akses.";
            return RedirectToAction("Index");
        }

        var pr = await _context.Pengajuan
            .FirstOrDefaultAsync(p => p.NoPR == noPR);

        // Hanya bisa upload bukti terima kalau PR sudah disetujui (status 3)
        if (pr == null || pr.Status != 3)
        {
            TempData["Error"] = "PR tidak ditemukan atau belum disetujui.";
            return RedirectToAction("Index");
        }

        if (fotoTerima == null || fotoTerima.Length == 0)
        {
            TempData["Error"] = "Foto bukti wajib diupload.";
            return RedirectToAction("Index");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(fotoTerima.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
        {
            TempData["Error"] = "File harus berformat JPG atau PNG.";
            return RedirectToAction("Index");
        }

        if (fotoTerima.Length > 5 * 1024 * 1024)
        {
            TempData["Error"] = "Ukuran file maksimal 5MB.";
            return RedirectToAction("Index");
        }

        var safeNoPR = string.Concat(noPR.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{safeNoPR}-terima{extension}";
        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "bukti");
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await fotoTerima.CopyToAsync(stream);

        pr.FotoTerima = $"/uploads/bukti/{fileName}";
        pr.TglTerima = DateTime.Now;

        // Status akhir tergantung jenis keputusan
        bool isService = pr.DecisionType == "SERVICE";
        pr.Status = isService ? 6 : 5; // 6 = Diservice, 5 = Diterima

        // Tandai PO terkait sebagai selesai
        var pesanan = await _context.Pesanan
            .FirstOrDefaultAsync(p => p.PR_ID == pr.PR_ID);
        if (pesanan != null) pesanan.Status = 2; // Selesai

        await _context.SaveChangesAsync();

        var statusText = isService ? "Diservice" : "Diterima";
        await _auditService.LogAsync(
            action: "KIRIM_BUKTI_TERIMA",
            entity: "Pengajuan",
            entityId: pr.PR_ID.ToString(),
            detail: $"Bukti barang {statusText}: {noPR} | File: {fileName}"
        );

        TempData["Success"] = $"Bukti dikirim. Status pengajuan diubah ke {statusText}.";
        return RedirectToAction("Index");
    }

    // ==================== INDEX ====================

    public async Task<IActionResult> Index(
    int page = 1,
    string? search = null,
    string? tanggalDari = null,
    string? tanggalSampai = null,
    int? status = null,
    string? urgency = null)
    {
        // Pastikan page selalu minimal 1 — mencegah OFFSET negatif
        if (page < 1) page = 1;

        int pageSize = 10;
        var role = HttpContext.Session.GetString("Role");
        var userID = HttpContext.Session.GetString("UserID");

        // TryParse lebih aman dari Parse — tidak throw exception kalau format salah
        DateTime? tglDari = DateTime.TryParse(tanggalDari, out var td) ? td : null;
        DateTime? tglSampai = DateTime.TryParse(tanggalSampai, out var ts) ? ts : null;

        var query = from pr in _context.Pengajuan
                    where role == "Manager" ||
                          (role == "Checker" && (pr.Status == 1 || pr.Status == 2 || pr.Status == 3 || pr.Status == 5 || pr.Status == 6)) ||
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
                    where string.IsNullOrEmpty(urgency) || pr.UrgencyLevel == urgency

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
                        TglFeedback = pr.TglFeedback,
                        UrgencyLevel = pr.UrgencyLevel,
                        Rekomendasi = pr.Rekomendasi,
                        DecisionType = pr.DecisionType,
                        HargaService = pr.HargaService,
                        FotoTerima = pr.FotoTerima,
                        TglTerima = pr.TglTerima
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
        ViewBag.FilterUrgency = urgency;

        return View(dataPaginated);
    }
}