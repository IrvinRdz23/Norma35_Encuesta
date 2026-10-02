using Microsoft.AspNetCore.Mvc;
using MahleSurvey.Services;

namespace MahleSurvey.Controllers;

public class ImportController(ExcelImportService importer, ILogger<ImportController> log) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Index(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("", "Seleccione un archivo para cargar.");
            return View();
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", "Sólo se aceptan archivos .xlsx. Si tiene un .xls, ábralo en Excel y guárdelo como .xlsx.");
            return View();
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await importer.ImportAsync(stream);
            return View("Result", result);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error importando {File}", file.FileName);
            ModelState.AddModelError("", $"No se pudo procesar el archivo: {ex.Message}");
            return View();
        }
    }
}
