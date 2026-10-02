using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MahleSurvey.Models;
using MahleSurvey.Options;
using MahleSurvey.Services;

namespace MahleSurvey.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SurveyController(
    EmployeeRepository employees,
    SurveyRepository surveys,
    IOptions<SurveyOptions> options,
    ILogger<SurveyController> log) : Controller
{
    private SurveyOptions O => options.Value;

    // ---------------- Anexo I ----------------
    [HttpGet]
    public IActionResult AnexoI() => View(new AnexoIViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AnexoI(AnexoIViewModel vm)
    {
        if (!await ResolveEmployeeAsync(vm)) return View(vm);

        var ids = AnexoIDefinition.All.Select(q => q.Id).ToList();
        bool Valid(int id) => vm.Answers.TryGetValue(id, out var v) && (v == O.YesValue || v == O.NoValue);

        foreach (var id in AnexoIDefinition.GateIds)
            if (!Valid(id)) vm.Missing.Add(id);

        var anyYes = AnexoIDefinition.GateIds.Any(id => vm.Answers.GetValueOrDefault(id) == O.YesValue);
        if (anyYes)
            foreach (var id in ids.Except(AnexoIDefinition.GateIds))
                if (!Valid(id)) vm.Missing.Add(id);

        if (vm.Missing.Count > 0)
        {
            ModelState.AddModelError("", "Responda todas las preguntas obligatorias. Las pendientes están resaltadas.");
            return View(vm);
        }

        try
        {
            var toSave = vm.Answers.Where(a => ids.Contains(a.Key) && Valid(a.Key));
            await surveys.SaveAnexoIAsync(vm.Employee!, toSave);
            return RedirectToAction(nameof(Completed));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error guardando Anexo I del empleado {Numero}", vm.NumeroReloj);
            ModelState.AddModelError("", "No se pudo guardar la encuesta. Intente de nuevo; si el problema continúa, contacte a soporte.");
            return View(vm);
        }
    }

    // ---------------- Anexo III ----------------
    [HttpGet]
    public async Task<IActionResult> AnexoIII()
    {
        var vm = new AnexoIIIViewModel();
        await LoadQuestionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AnexoIII(AnexoIIIViewModel vm)
    {
        await LoadQuestionsAsync(vm);
        if (!await ResolveEmployeeAsync(vm)) return View(vm);

        var toSave = new List<(int, string, int)>();
        foreach (var q in vm.Questions)
        {
            var required = !vm.DependentOf.TryGetValue(q.Id, out var gate)
                           || vm.Answers.GetValueOrDefault(gate) == O.YesValue;
            if (!required) continue;

            var opt = vm.Answers.TryGetValue(q.Id, out var v) ? q.Options.FirstOrDefault(o => o.Value == v) : null;
            if (opt is null) vm.Missing.Add(q.Id);
            else toSave.Add((q.Id, opt.Text, opt.Value));
        }

        if (vm.Missing.Count > 0)
        {
            ModelState.AddModelError("", "Responda todas las preguntas obligatorias. Las pendientes están resaltadas.");
            return View(vm);
        }

        try
        {
            await surveys.SaveAnexoIIIAsync(vm.Employee!, toSave);
            return RedirectToAction(nameof(Completed));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error guardando Anexo III del empleado {Numero}", vm.NumeroReloj);
            ModelState.AddModelError("", "No se pudo guardar la encuesta. Intente de nuevo; si el problema continúa, contacte a soporte.");
            return View(vm);
        }
    }

    public IActionResult Completed() => View();

    // ---------------- helpers ----------------
    private async Task LoadQuestionsAsync(AnexoIIIViewModel vm)
    {
        try { vm.Questions = await surveys.GetQuestionsAsync(); }
        catch (Exception ex)
        {
            log.LogError(ex, "Error cargando preguntas del Anexo III");
            ModelState.AddModelError("", "No se pudieron cargar las preguntas. Contacte a soporte.");
        }
        foreach (var g in O.Gates)
            foreach (var d in g.Dependents) vm.DependentOf[d] = g.Question;
    }

    private async Task<bool> ResolveEmployeeAsync(SurveyViewModel vm)
    {
        vm.NumeroReloj = vm.NumeroReloj?.Trim();
        if (string.IsNullOrEmpty(vm.NumeroReloj) || !Regex.IsMatch(vm.NumeroReloj, @"^\d{8}$"))
        {
            ModelState.AddModelError("", "El número de reloj debe ser numérico y de 8 dígitos.");
            return false;
        }
        try
        {
            vm.Employee = await employees.FindAsync(vm.NumeroReloj);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error validando empleado {Numero}", vm.NumeroReloj);
            ModelState.AddModelError("", "No se pudo consultar la base de datos. Intente de nuevo.");
            return false;
        }
        if (vm.Employee is null)
        {
            ModelState.AddModelError("", "No se encontró un empleado con ese número de reloj.");
            return false;
        }
        return true;
    }
}
