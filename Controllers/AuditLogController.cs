using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProflowApp.Data;

namespace ProFlowApp.Controllers;

public class AuditLogController : BaseController
{
    private readonly ApplicationDbContext _context;

    public AuditLogController(ApplicationDbContext context)
    {
        _context = context;
    }

    private IActionResult? RedirectIfNotManager()
    {
        if (HttpContext.Session.GetString("Role") != "Manager")
            return RedirectToAction("Index", "Home");
        return null;
    }

    public async Task<IActionResult> Index(
        int page = 1,
        string? search = null,
        string? tipeAktivitas = null,
        string? tanggalDari = null,
        string? tanggalSampai = null)
    {
        var redirect = RedirectIfNotManager();
        if (redirect != null) return redirect;

        if (page < 1) page = 1;

        DateTime? tglDari   = DateTime.TryParse(tanggalDari,   out var td) ? td : null;
        DateTime? tglSampai = DateTime.TryParse(tanggalSampai, out var ts) ? ts : null;

        var query = _context.AuditLogs
            .Where(a => string.IsNullOrEmpty(search) ||
                        (a.Username != null && a.Username.Contains(search)) ||
                        (a.Detail   != null && a.Detail.Contains(search)))
            .Where(a => string.IsNullOrEmpty(tipeAktivitas) || a.Action == tipeAktivitas)
            .Where(a => tglDari   == null || a.Timestamp >= tglDari)
            .Where(a => tglSampai == null || a.Timestamp <= tglSampai.Value.Date.AddDays(1).AddSeconds(-1))
            .OrderByDescending(a => a.Timestamp);

        int pageSize   = 15;
        int totalData  = await query.CountAsync();
        int totalPages = (int)Math.Ceiling((double)totalData / pageSize);

        if (page > totalPages && totalPages > 0) page = totalPages;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var allActions = await _context.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        ViewBag.CurrentPage         = page;
        ViewBag.TotalPages          = totalPages;
        ViewBag.TotalData           = totalData;
        ViewBag.FilterSearch        = search;
        ViewBag.FilterTipeAktivitas = tipeAktivitas;
        ViewBag.FilterTanggalDari   = tanggalDari;
        ViewBag.FilterTanggalSampai = tanggalSampai;
        ViewBag.AllActions          = allActions;

        return View(items);
    }
}