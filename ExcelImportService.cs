using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using MahleSurvey.Models;
using MahleSurvey.Options;

namespace MahleSurvey.Services;

public class ExcelImportService(SurveyRepository surveys, IOptions<SurveyOptions> options)
{
    private SurveyOptions O => options.Value;

    public async Task<ImportResult> ImportAsync(Stream file)
    {
        var result = new ImportResult();
        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheets.First();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        var gates = O.Gates.ToDictionary(g => g.Question, g => g.Dependents.ToHashSet());
        var byEmployee = new Dictionary<string, List<ImportRow>>();

        for (var r = 2; r <= lastRow; r++)
        {
            var idCell = ws.Cell(r, O.ExcelEmployeeColumn);
            var emp = idCell.DataType == XLDataType.Number
                ? ((long)idCell.GetDouble()).ToString()
                : idCell.GetString().Trim();

            if (emp.Length == 0) continue;
            if (!Regex.IsMatch(emp, @"^\d{8}$"))
            {
                result.Advertencias.Add($"Fila {r}: número de reloj '{emp}' inválido (deben ser 8 dígitos). Fila omitida.");
                continue;
            }

            var rows = new List<ImportRow>();
            var skip = new HashSet<int>();

            for (var col = O.ExcelFirstQuestionColumn; col <= O.ExcelLastQuestionColumn; col++)
            {
                var q = col - O.ExcelFirstQuestionColumn + 1;
                if (skip.Contains(q)) continue;

                var cell = ws.Cell(r, col);
                var text = cell.IsEmpty() ? "" : cell.GetString();
                var code = AnswerLabels.Code(text, out var skipRest);

                if (text.Length > 0 && code == 0)
                    result.Advertencias.Add($"Fila {r}, pregunta {q}: respuesta no reconocida '{text}'. Se guardó como 0.");

                rows.Add(new ImportRow { NumeroReloj = emp, Pregunta = q, Valor = code });

                if (gates.TryGetValue(q, out var deps) && code == O.NoValue)
                    skip.UnionWith(deps);
                else if (skipRest)
                    for (var k = 1; k <= 4; k++) skip.Add(q + k);
            }

            byEmployee[emp] = rows; // si el empleado aparece repetido, gana la última fila
        }

        var all = byEmployee.Values.SelectMany(x => x).ToList();
        await surveys.ReplaceImportedAsync(all);

        result.Empleados = byEmployee.Count;
        result.Filas = all.Count;
        return result;
    }
}
