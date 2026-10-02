using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MahleSurvey.Controllers;

public class HomeController : Controller
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Index() => View();

    public IActionResult About() => View();
    public IActionResult Contact() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        ViewBag.Code = code;
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
