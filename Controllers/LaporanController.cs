using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProflowApp.Data;
using ProFlowApp.ViewModels;

namespace ProFlowApp.Controllers
{
    [Route("Laporan")]
    public class LaporanController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        // SSRS_BASE di-hardcode di sini — tidak boleh dari input user
        // Ini mencegah attacker mengarahkan request ke server lain
        private const string SSRS_BASE = "http://jepaygon-pc/ReportServer/Pages/ReportViewer.aspx";

        // Whitelist format yang diizinkan — HANYA 2 nilai ini
        // Mencegah attacker inject nilai lain di parameter format
        private static readonly HashSet<string> ALLOWED_FORMATS = new()
        {
            "PDF",
            "EXCELOPENXML"
        };

        // Whitelist report path yang diizinkan
        // Mencegah attacker akses report lain di SSRS server
        private static readonly HashSet<string> ALLOWED_REPORTS = new()
        {
            "/ProFlow/report_pr3",
            "/ProFlow/report_po"
        };

        public LaporanController(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration config)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        private IActionResult? RedirectIfNotManager()
        {
            if (HttpContext.Session.GetString("Role") != "Manager")
                return RedirectToAction("Index", "Home");
            return null;
        }

        private HttpClient CreateSsrsClient()
        {
            var handler = new HttpClientHandler
            {
                Credentials = new System.Net.NetworkCredential(
                    _config["Ssrs:Username"],
                    _config["Ssrs:Password"],
                    _config["Ssrs:Domain"]
                ),
                PreAuthenticate = true
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        }

        // Method validasi terpusat
        // Semua validasi parameter SSRS ada di sini
        // Return null kalau valid, return pesan error kalau tidak valid
        private string? ValidateSsrsParams(
            string? format,
            string? tanggalDari,
            string? tanggalSampai,
            string? status,
            string? reportPath)
        {
            // 1. Validasi format — hanya PDF atau EXCELOPENXML
            if (!string.IsNullOrEmpty(format) && !ALLOWED_FORMATS.Contains(format))
                return $"Format tidak valid: {format}";

            // 2. Validasi report path — hanya path yang ada di whitelist
            if (!string.IsNullOrEmpty(reportPath) && !ALLOWED_REPORTS.Contains(reportPath))
                return $"Report tidak valid.";

            // 3. Validasi tanggal — harus format yyyy-MM-dd yang valid
            // Mencegah injection melalui parameter tanggal
            if (!string.IsNullOrEmpty(tanggalDari) &&
                !DateTime.TryParseExact(tanggalDari, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
                return "Format tanggal dari tidak valid.";

            if (!string.IsNullOrEmpty(tanggalSampai) &&
                !DateTime.TryParseExact(tanggalSampai, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
                return "Format tanggal sampai tidak valid.";

            // 4. Validasi status — harus angka saja
            // Mencegah injection melalui parameter status
            if (!string.IsNullOrEmpty(status) && !int.TryParse(status, out _))
                return "Status tidak valid.";

            return null; // null = semua valid
        }

        // LAPORAN PR
        [HttpGet("Pengajuan")]
        public async Task<IActionResult> Pengajuan(
            DateTime? tanggalDari,
            DateTime? tanggalSampai,
            int? status,
            string namaKaryawan,
            string tipeGrafik = "tahun",
            bool submitted = false)
        {
            var redirect = RedirectIfNotManager();
            if (redirect != null) return redirect;

            var vm = new LaporanPRViewModel
            {
                TanggalDari = tanggalDari,
                TanggalSampai = tanggalSampai,
                Status = status,
                NamaKaryawan = namaKaryawan,
                TipeGrafik = tipeGrafik
            };

            var rawQuery = await _context.Pengajuan
                .Join(_context.Users, p => p.UserID, u => u.UserID, (p, u) => new { p, u })
                .Join(_context.Barang, pu => pu.p.Brg_ID, b => b.Brg_ID, (pu, b) => new { pu.p, pu.u, b })
                .Where(x =>
                    (tanggalDari == null || x.p.Tgl_Req >= tanggalDari) &&
                    (tanggalSampai == null || x.p.Tgl_Req <= tanggalSampai.Value.Date.AddDays(1).AddSeconds(-1)) &&
                    (status == null ||
                        (status == 0 && new[] { 0, 1, 2 }.Contains(x.p.Status ?? -1)) ||
                        (status == 3 && x.p.Status == 3) ||
                        (status == 4 && x.p.Status == 4)) &&
                    (string.IsNullOrEmpty(namaKaryawan) || x.u.Nama.Contains(namaKaryawan)))
                .OrderByDescending(x => x.p.Tgl_Req)
                .ToListAsync();

            vm.Items = rawQuery.Select((x, i) => new LaporanPRItem
            {
                No = i + 1,
                NoPR = x.p.NoPR,
                TanggalPR = x.p.Tgl_Req,
                NamaKaryawan = x.u.Nama,
                NamaBarang = x.b.Nm_Brg,
                NamaVendor = x.b.Nm_Vendor,
                Satuan = x.b.Satuan,
                Jumlah = x.p.Jml,
                HargaSatuan = x.b.Hrg_Est,
                TotalHarga = x.p.Jml * x.b.Hrg_Est,
                Status = x.p.Status
            }).ToList();

            vm.TotalPR = vm.Items.Count;
            vm.TotalPending = vm.Items.Count(x => x.Status.HasValue && new[] { 0, 1, 2 }.Contains(x.Status.Value));
            vm.TotalDisetujui = vm.Items.Count(x => x.Status.HasValue && x.Status.Value == 3);
            vm.TotalDitolak = vm.Items.Count(x => x.Status.HasValue && x.Status.Value == 4);
            vm.GrandTotal = vm.Items.Sum(x => x.TotalHarga ?? 0);

            var grafikRaw = await _context.Pengajuan
                .Join(_context.Users, p => p.UserID, u => u.UserID, (p, u) => new { p, u })
                .Where(x =>
                    (tanggalDari == null || x.p.Tgl_Req >= tanggalDari) &&
                    (tanggalSampai == null || x.p.Tgl_Req <= tanggalSampai.Value.Date.AddDays(1).AddSeconds(-1)) &&
                    (status == null ||
                        (status == 0 && new[] { 0, 1, 2 }.Contains(x.p.Status ?? -1)) ||
                        (status == 3 && x.p.Status == 3) ||
                        (status == 4 && x.p.Status == 4)) &&
                    (string.IsNullOrEmpty(namaKaryawan) || x.u.Nama.Contains(namaKaryawan)))
                .Select(x => new { x.p.Tgl_Req, x.p.Status })
                .ToListAsync();

            if (tipeGrafik == "tahun")
            {
                vm.DataGrafik = grafikRaw
                    .GroupBy(x => new { x.Tgl_Req.Year, x.Tgl_Req.Month })
                    .Select(g => new GrafikItem
                    {
                        Tahun = g.Key.Year,
                        Bulan = g.Key.Month,
                        NamaBulan = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                        MingguKe = 0,
                        JumlahPR = g.Count(),
                        JumlahPending = g.Count(x => x.Status.HasValue && new[] { 0, 1, 2 }.Contains(x.Status.Value)),
                        JumlahDisetujui = g.Count(x => x.Status.HasValue && x.Status.Value == 3),
                        JumlahDitolak = g.Count(x => x.Status.HasValue && x.Status.Value == 4)
                    })
                    .OrderBy(x => x.Tahun).ThenBy(x => x.Bulan)
                    .ToList();
            }
            else
            {
                vm.DataGrafik = grafikRaw
                    .GroupBy(x => new { x.Tgl_Req.Year, x.Tgl_Req.Month, Week = (x.Tgl_Req.Day - 1) / 7 + 1 })
                    .Select(g => new GrafikItem
                    {
                        Tahun = g.Key.Year,
                        Bulan = g.Key.Month,
                        NamaBulan = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                        MingguKe = g.Key.Week,
                        JumlahPR = g.Count(),
                        JumlahPending = g.Count(x => x.Status.HasValue && new[] { 0, 1, 2 }.Contains(x.Status.Value)),
                        JumlahDisetujui = g.Count(x => x.Status.HasValue && x.Status.Value == 3),
                        JumlahDitolak = g.Count(x => x.Status.HasValue && x.Status.Value == 4)
                    })
                    .OrderBy(x => x.Tahun).ThenBy(x => x.Bulan).ThenBy(x => x.MingguKe)
                    .ToList();
            }

            return View(vm);
        }

        [HttpGet("ExportPR")]
        public async Task<IActionResult> ExportPR(
            string tanggalDari, string tanggalSampai, string status,
            string namaKaryawan, string tipeGrafik = "tahun", string format = "PDF")
        {
            var redirect = RedirectIfNotManager();
            if (redirect != null) return redirect;

            // Validasi semua parameter sebelum dipakai
            // Kalau ada yang tidak valid → tolak request
            var reportPath = "/ProFlow/report_pr3";
            var validationError = ValidateSsrsParams(
                format, tanggalDari, tanggalSampai, status, reportPath);

            if (validationError != null)
            {
                TempData["Error"] = validationError;
                return RedirectToAction(nameof(Pengajuan));
            }

            try
            {
                // Bangun URL dari komponen yang sudah divalidasi
                // Tidak ada string dari user yang langsung masuk ke URL
                // tanpa validasi terlebih dahulu
                var url = $"{SSRS_BASE}?{reportPath}" +
                          $"&rs:Command=Render" +
                          $"&rs:Format={format}" +
                          $"&rc:Parameters=false";

                // Tanggal sudah divalidasi format yyyy-MM-dd
                // Tambah waktu setelah validasi — bukan dari input user
                if (!string.IsNullOrEmpty(tanggalDari))
                    url += $"&TanggalDari={tanggalDari}T00:00:00";

                if (!string.IsNullOrEmpty(tanggalSampai))
                    url += $"&TanggalSampai={tanggalSampai}T23:59:59";

                // Status sudah divalidasi angka saja
                if (!string.IsNullOrEmpty(status))
                    url += $"&Status={status}";

                // NamaKaryawan di-escape untuk mencegah injection
                if (!string.IsNullOrEmpty(namaKaryawan))
                    url += $"&NamaKaryawan={Uri.EscapeDataString(namaKaryawan)}";

                // TipeGrafik — hanya 2 nilai valid, hardcode whitelist
                var safeTipeGrafik = tipeGrafik == "bulan" ? "bulan" : "tahun";
                url += $"&TipeGrafik={safeTipeGrafik}";

                using var client = CreateSsrsClient();
                var bytes = await client.GetByteArrayAsync(url);

                var contentType = format == "EXCELOPENXML"
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    : "application/pdf";

                var fileName = format == "EXCELOPENXML" ? "LaporanPR.xlsx" : "LaporanPR.pdf";
                return File(bytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Gagal export: {ex.Message}";
                return RedirectToAction(nameof(Pengajuan));
            }
        }

        // LAPORAN PO

        [HttpGet("Pesanan")]
        public async Task<IActionResult> Pesanan(
            DateTime? tanggalDari,
            DateTime? tanggalSampai,
            int? status,
            string namaKaryawan,
            string tipeGrafik = "tahun",
            bool submitted = false)
        {
            var redirect = RedirectIfNotManager();
            if (redirect != null) return redirect;

            var vm = new LaporanPOViewModel
            {
                TanggalDari = tanggalDari,
                TanggalSampai = tanggalSampai,
                Status = status,
                NamaKaryawan = namaKaryawan,
                TipeGrafik = tipeGrafik
            };

            var rawQuery = await _context.Pesanan
                .Join(_context.Pengajuan, po => po.PR_ID, pr => pr.PR_ID, (po, pr) => new { po, pr })
                .Join(_context.Users, x => x.pr.UserID, u => u.UserID, (x, u) => new { x.po, x.pr, u })
                .Join(_context.Barang, x => x.pr.Brg_ID, b => b.Brg_ID, (x, b) => new { x.po, x.pr, x.u, b })
                .Where(x =>
                    (tanggalDari == null || x.po.tgl_PO >= tanggalDari) &&
                    (tanggalSampai == null || x.po.tgl_PO <= tanggalSampai.Value.Date.AddDays(1).AddSeconds(-1)) &&
                    (status == null || x.po.Status == status) &&
                    (string.IsNullOrEmpty(namaKaryawan) || x.u.Nama.Contains(namaKaryawan)))
                .OrderByDescending(x => x.po.tgl_PO)
                .ToListAsync();

            vm.Items = rawQuery.Select((x, i) => new LaporanPOItem
            {
                No = i + 1,
                NoPO = x.po.NoPO,
                NoPR = x.pr.NoPR,
                TanggalPO = x.po.tgl_PO,
                NamaKaryawan = x.u.Nama,
                NamaBarang = x.b.Nm_Brg,
                NamaVendor = x.b.Nm_Vendor,
                Satuan = x.b.Satuan,
                Jumlah = x.pr.Jml,
                HargaSatuan = x.b.Hrg_Est,
                TotalHarga = x.pr.Jml * x.b.Hrg_Est,
                Status = x.po.Status
            }).ToList();

            vm.TotalPO = vm.Items.Count;
            vm.TotalPending = vm.Items.Count(x => x.Status.HasValue && x.Status.Value == 0);
            vm.TotalDiproses = vm.Items.Count(x => x.Status.HasValue && x.Status.Value == 1);
            vm.TotalSelesai = vm.Items.Count(x => x.Status.HasValue && x.Status.Value == 2);
            vm.GrandTotal = vm.Items.Sum(x => x.TotalHarga ?? 0);

            var grafikRaw = await _context.Pesanan
                .Join(_context.Pengajuan, po => po.PR_ID, pr => pr.PR_ID, (po, pr) => new { po, pr })
                .Join(_context.Users, x => x.pr.UserID, u => u.UserID, (x, u) => new { x.po, x.pr, u })
                .Where(x =>
                    (tanggalDari == null || x.po.tgl_PO >= tanggalDari) &&
                    (tanggalSampai == null || x.po.tgl_PO <= tanggalSampai.Value.Date.AddDays(1).AddSeconds(-1)) &&
                    (status == null || x.po.Status == status) &&
                    (string.IsNullOrEmpty(namaKaryawan) || x.u.Nama.Contains(namaKaryawan)))
                .Select(x => new { x.po.tgl_PO, x.po.Status })
                .ToListAsync();

            if (tipeGrafik == "tahun")
            {
                vm.DataGrafik = grafikRaw
                    .GroupBy(x => new { x.tgl_PO.Year, x.tgl_PO.Month })
                    .Select(g => new GrafikPOItem
                    {
                        Tahun = g.Key.Year,
                        Bulan = g.Key.Month,
                        NamaBulan = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                        MingguKe = 0,
                        JumlahPO = g.Count(),
                        JumlahPending = g.Count(x => x.Status.HasValue && x.Status.Value == 0),
                        JumlahDiproses = g.Count(x => x.Status.HasValue && x.Status.Value == 1),
                        JumlahSelesai = g.Count(x => x.Status.HasValue && x.Status.Value == 2)
                    })
                    .OrderBy(x => x.Tahun).ThenBy(x => x.Bulan)
                    .ToList();
            }
            else
            {
                vm.DataGrafik = grafikRaw
                    .GroupBy(x => new { x.tgl_PO.Year, x.tgl_PO.Month, Week = (x.tgl_PO.Day - 1) / 7 + 1 })
                    .Select(g => new GrafikPOItem
                    {
                        Tahun = g.Key.Year,
                        Bulan = g.Key.Month,
                        NamaBulan = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMEM"),
                        MingguKe = g.Key.Week,
                        JumlahPO = g.Count(),
                        JumlahPending = g.Count(x => x.Status.HasValue && x.Status.Value == 0),
                        JumlahDiproses = g.Count(x => x.Status.HasValue && x.Status.Value == 1),
                        JumlahSelesai = g.Count(x => x.Status.HasValue && x.Status.Value == 2)
                    })
                    .OrderBy(x => x.Tahun).ThenBy(x => x.Bulan).ThenBy(x => x.MingguKe)
                    .ToList();
            }

            return View(vm);
        }

        [HttpGet("ExportPO")]
        public async Task<IActionResult> ExportPO(
            string tanggalDari, string tanggalSampai, string status,
            string namaKaryawan, string tipeGrafik = "tahun", string format = "PDF")
        {
            var redirect = RedirectIfNotManager();
            if (redirect != null) return redirect;

            // Validasi semua parameter sebelum dipakai
            var reportPath = "/ProFlow/report_po";
            var validationError = ValidateSsrsParams(
                format, tanggalDari, tanggalSampai, status, reportPath);

            if (validationError != null)
            {
                TempData["Error"] = validationError;
                return RedirectToAction(nameof(Pesanan));
            }

            try
            {
                var url = $"{SSRS_BASE}?{reportPath}" +
                          $"&rs:Command=Render" +
                          $"&rs:Format={format}" +
                          $"&rc:Parameters=false";

                if (!string.IsNullOrEmpty(tanggalDari))
                    url += $"&TanggalDari={tanggalDari}T00:00:00";

                if (!string.IsNullOrEmpty(tanggalSampai))
                    url += $"&TanggalSampai={tanggalSampai}T23:59:59";

                if (!string.IsNullOrEmpty(status))
                    url += $"&Status={status}";

                if (!string.IsNullOrEmpty(namaKaryawan))
                    url += $"&NamaKaryawan={Uri.EscapeDataString(namaKaryawan)}";

                var safeTipeGrafik = tipeGrafik == "bulan" ? "bulan" : "tahun";
                url += $"&TipeGrafik={safeTipeGrafik}";

                using var client = CreateSsrsClient();
                var bytes = await client.GetByteArrayAsync(url);

                var contentType = format == "EXCELOPENXML"
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    : "application/pdf";

                var fileName = format == "EXCELOPENXML" ? "LaporanPO.xlsx" : "LaporanPO.pdf";
                return File(bytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Gagal export: {ex.Message}";
                return RedirectToAction(nameof(Pesanan));
            }
        }
    }
}