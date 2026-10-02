using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using MahleSurvey.Models;
using MahleSurvey.Options;
using MahleSurvey.Services;

namespace MahleSurvey.Controllers;

public class ReportsController(
    ReportRepository repo,
    ExcelExportService excel,
    IOptions<SurveyOptions> options,
    ILogger<ReportsController> log) : Controller
{
    public async Task<IActionResult> Index()
    {
        var codes = await repo.GetAnexosAsync();
        var items = codes.Select(c => new SelectListItem(options.Value.LabelFor(c), c)).ToList();
        return View(items);
    }

    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Data([FromQuery] ReportFilter filter)
    {
        try { return Json(await repo.GetDashboardAsync(filter)); }
        catch (Exception ex)
        {
            log.LogError(ex, "Error generando datos del dashboard");
            return Problem("No se pudieron generar los datos del reporte.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] ReportFilter filter)
    {
        try
        {
            var dash = await repo.GetDashboardAsync(filter);
            var raw = await repo.GetRawAsync(filter);
            if (raw.Count == 0)
            {
                TempData["Error"] = "No se encontraron registros para generar el reporte.";
                return RedirectToAction(nameof(Index));
            }
            var bytes = excel.Build(dash, raw, filter);
            var name = $"ReporteEncuestas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error exportando a Excel");
            TempData["Error"] = "Ocurrió un error al generar el Excel. Revise el log del servidor.";
            return RedirectToAction(nameof(Index));
        }
    }
}
