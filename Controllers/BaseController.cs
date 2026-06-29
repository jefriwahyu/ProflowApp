using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ProFlowApp.Controllers;

public class BaseController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        var actionName = context.RouteData.Values["action"]?.ToString();

        if (controllerName == "Account")
        {
            base.OnActionExecuting(context);
            return;
        }

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
        }

        if (controllerName == "Pengajuan" && actionName == "Create")
        {
            var role = HttpContext.Session.GetString("Role");

            // Hanya Karyawan yang bisa akses Create
            if (role != "Karyawan")
            {
                TempData["Error"] = "Hanya karyawan yang dapat membuat pengajuan.";
                context.Result = new RedirectToActionResult("Index", "Pengajuan", null);
                return;
            }
        }

        base.OnActionExecuting(context);
    }
}