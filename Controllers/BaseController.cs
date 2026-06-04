using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ProFlowApp.Controllers;

public class BaseController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        if (controllerName == "Account")
        {
            base.OnActionExecuting(context);
            return;
        }

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
        }

        base.OnActionExecuting(context);
    }
}