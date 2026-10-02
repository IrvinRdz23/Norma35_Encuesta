using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using MahleSurvey.Services;

namespace MahleSurvey.Controllers;

public class EmployeeController(EmployeeRepository employees, ILogger<EmployeeController> log) : Controller
{
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Lookup(string? numero)
    {
        numero = numero?.Trim() ?? "";
        if (!Regex.IsMatch(numero, @"^\d{8}$"))
            return Json(new { found = false, message = "El número de reloj debe tener 8 dígitos." });

        try
        {
            var e = await employees.FindAsync(numero);
            if (e is null) return Json(new { found = false, message = "No se encontró un empleado con ese número de reloj." });

            return Json(new
            {
                found = true,
                nombre = e.Nombre, turno = e.Turno, puesto = e.Puesto, area = e.Area,
                planta = e.Planta, region = e.Region, sexo = e.Sexo, fechaNacimiento = e.FechaTexto
            });
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error buscando empleado {Numero}", numero);
            return StatusCode(500, new { found = false, message = "No se pudo consultar la base de datos. Intente de nuevo." });
        }
    }
}
