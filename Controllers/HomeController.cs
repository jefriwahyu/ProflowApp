using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProFlowApp.Models;

namespace ProFlowApp.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // Handle error umum (500) — dipanggil oleh UseExceptionHandler
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View("Error500");
    }

    // Handle status code spesifik (404, dll) — dipanggil oleh UseStatusCodePagesWithReExecute
    [Route("Home/Error/{statusCode}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int statusCode)
    {
        if (statusCode == 404)
            return View("Error404");

        return View("Error500");
    }

    // [Route("test-error")]
    // public IActionResult TestError()
    // {
    //     throw new Exception("Test error 500");
    // }
}
