using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using MahleSurvey.Models;
using MahleSurvey.Options;

namespace MahleSurvey.Services;

public class ExcelExportService(IOptions<SurveyOptions> options)
{
    private SurveyOptions O => options.Value;
    private static readonly XLColor Navy = XLColor.FromHtml("#0B2A4A");
    private static readonly XLColor Zebra = XLColor.FromHtml("#F2F6FA");

    public byte[] Build(DashboardData d, List<RawAnswer> raw, ReportFilter f)
    {
        using var wb = new XLWorkbook();
        BuildSummary(wb, d, f);
        BuildByQuestion(wb, d);
        foreach (var g in raw.GroupBy(x => x.Anexo).OrderBy(g => g.Key))
            BuildAnswers(wb, g.Key, g.ToList());

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private void BuildSummary(XLWorkbook wb, DashboardData d, ReportFilter f)
    {
        var ws = wb.AddWorksheet("Resumen");
        ws.ShowGridLines = false;
        ws.Column(1).Width = 3;
        ws.Column(2).Width = 34;
        for (var i = 3; i <= 6; i++) ws.Column(i).Width = 16;

        var t = ws.Cell(2, 2);
        t.Value = "MAHLE · Reporte de encuestas NOM-035";
        t.Style.Font.Bold = true; t.Style.Font.FontSize = 18; t.Style.Font.FontColor = Navy;

        var filtro = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Anexo)) filtro.Add(O.LabelFor(f.Anexo));
        if (f.Desde.HasValue) filtro.Add($"desde {f.Desde:dd/MM/yyyy}");
        if (f.Hasta.HasValue) filtro.Add($"hasta {f.Hasta:dd/MM/yyyy}");
        ws.Cell(3, 2).Value = $"Generado {DateTime.Now:dd/MM/yyyy HH:mm}. Filtro: {(filtro.Count == 0 ? "todos los registros" : string.Join(", ", filtro))}";
        ws.Cell(3, 2).Style.Font.FontColor = XLColor.Gray;

        var r = 5;
        r = Table(ws, r, "Indicadores", ["Indicador", "Valor"],
        [
            ["Plantilla registrada", d.Plantilla],
            ["Empleados encuestados", d.Totales.Empleados],
            ["Cobertura (%)", d.Cobertura],
            ["Respuestas registradas", d.Totales.Respuestas],
            ["Última captura", d.Totales.Ultima?.ToString("dd/MM/yyyy HH:mm") ?? "—"],
        ]);

        if (d.AnexoI is { } a1)
            r = Table(ws, r, "Anexo I · Acontecimientos traumáticos severos", ["Indicador", "Valor"],
            [
                ["Trabajadores que respondieron", a1.Total],
                ["Con al menos un acontecimiento (sección I)", a1.ConEvento],
                ["Porcentaje (%)", a1.Pct],
            ]);

        if (d.Riesgo.Count > 0)
            r = Table(ws, r, "Anexo III · Nivel de riesgo por empleado", ["Nivel", "Empleados"],
                d.Riesgo.Select(x => new object?[] { x.Etiqueta, x.Valor }).ToList());

        r = Table(ws, r, "Participación por área", ["Área", "Plantilla", "Encuestados", "Cobertura (%)"],
            d.PorArea.Select(x => new object?[] { x.Etiqueta, x.Plantilla, x.Encuestados, x.Pct }).ToList());

        Table(ws, r, "Participación por turno", ["Turno", "Plantilla", "Encuestados", "Cobertura (%)"],
            d.PorTurno.Select(x => new object?[] { x.Etiqueta, x.Plantilla, x.Encuestados, x.Pct }).ToList());
    }

    private void BuildByQuestion(XLWorkbook wb, DashboardData d)
    {
        if (d.Preguntas.Count == 0) return;
        var ws = wb.AddWorksheet("Por pregunta");
        string[] headers = ["Anexo", "Pregunta", "Texto", .. AnswerLabels.Order, "Total"];
        Header(ws, 1, headers);

        var r = 2;
        foreach (var q in d.Preguntas)
        {
            ws.Cell(r, 1).Value = q.Anexo;
            ws.Cell(r, 2).Value = q.Numero;
            ws.Cell(r, 3).Value = q.Texto ?? "";
            for (var i = 0; i < AnswerLabels.Order.Length; i++)
                ws.Cell(r, 4 + i).Value = q.Conteos.GetValueOrDefault(AnswerLabels.Order[i]);
            ws.Cell(r, headers.Length).Value = q.Conteos.Values.Sum();
            if (r % 2 == 1) ws.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = Zebra;
            r++;
        }
        ws.Column(3).Width = 70;
        ws.Column(3).Style.Alignment.WrapText = true;
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, r - 1, headers.Length).SetAutoFilter();
    }

    private void BuildAnswers(XLWorkbook wb, string code, List<RawAnswer> rows)
    {
        var qs = rows.Select(x => AnswerLabels.QuestionNumber(x.Pregunta))
            .Where(n => n.HasValue).Select(n => n!.Value).Distinct().OrderBy(n => n).ToList();
        var colOf = qs.Select((n, i) => (n, i)).ToDictionary(t => t.n, t => t.i + 7);

        var name = Regex.Replace($"Respuestas {code}", @"[\\/*?:\[\]]", "");
        var ws = wb.AddWorksheet(name.Length > 31 ? name[..31] : name);

        string[] fixedHeaders = ["Núm. reloj", "Nombre", "Área", "Turno", "Puesto", "Última actualización"];
        Header(ws, 1, [.. fixedHeaders, .. qs.Select(n => $"P{n}")]);

        var r = 2;
        foreach (var emp in rows.GroupBy(x => x.NumeroReloj).OrderBy(g => g.Key))
        {
            var first = emp.First();
            ws.Cell(r, 1).Value = emp.Key;
            ws.Cell(r, 2).Value = first.Nombre ?? "";
            ws.Cell(r, 3).Value = first.Area ?? "";
            ws.Cell(r, 4).Value = first.Turno ?? "";
            ws.Cell(r, 5).Value = first.Puesto ?? "";
            var max = emp.Max(x => x.Fecha);
            if (max.HasValue) { ws.Cell(r, 6).Value = max.Value; ws.Cell(r, 6).Style.DateFormat.Format = "dd/mm/yyyy hh:mm"; }

            foreach (var a in emp)
            {
                var n = AnswerLabels.QuestionNumber(a.Pregunta);
                if (n is null) continue;
                ws.Cell(r, colOf[n.Value]).Value = AnswerLabels.Label(a.Respuesta) ?? "";
            }
            r++;
        }

        var lastCol = fixedHeaders.Length + qs.Count;
        ws.Column(1).Width = 13; ws.Column(2).Width = 32; ws.Column(3).Width = 24;
        ws.Column(4).Width = 10; ws.Column(5).Width = 28; ws.Column(6).Width = 20;
        for (var c = 7; c <= lastCol; c++) ws.Column(c).Width = 14;
        ws.SheetView.FreezeRows(1);
        ws.SheetView.FreezeColumns(2);
        if (r > 2) ws.Range(1, 1, r - 1, lastCol).SetAutoFilter();
    }

    private static void Header(IXLWorksheet ws, int row, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var h = ws.Cell(row, i + 1);
            h.Value = headers[i];
            h.Style.Font.Bold = true;
            h.Style.Font.FontColor = XLColor.White;
            h.Style.Fill.BackgroundColor = Navy;
            h.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            h.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(row).Height = 22;
    }

    private static int Table(IXLWorksheet ws, int row, string title, string[] headers, List<object?[]> data)
    {
        var t = ws.Cell(row, 2);
        t.Value = title;
        t.Style.Font.Bold = true; t.Style.Font.FontSize = 12; t.Style.Font.FontColor = Navy;
        row++;

        for (var i = 0; i < headers.Length; i++)
        {
            var h = ws.Cell(row, 2 + i);
            h.Value = headers[i];
            h.Style.Font.Bold = true; h.Style.Font.FontColor = XLColor.White;
            h.Style.Fill.BackgroundColor = Navy;
            h.Style.Alignment.Horizontal = i == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Center;
        }
        row++;
        var first = row;

        foreach (var rowData in data)
        {
            for (var i = 0; i < rowData.Length; i++) Put(ws.Cell(row, 2 + i), rowData[i]);
            if ((row - first) % 2 == 1) ws.Range(row, 2, row, 1 + headers.Length).Style.Fill.BackgroundColor = Zebra;
            row++;
        }
        if (data.Count > 0)
            ws.Range(first - 1, 2, row - 1, 1 + headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        return row + 1;
    }

    private static void Put(IXLCell c, object? v)
    {
        switch (v)
        {
            case null: break;
            case int i: c.Value = i; break;
            case long l: c.Value = l; break;
            case double x: c.Value = x; c.Style.NumberFormat.Format = "0.0"; break;
            default: c.Value = v.ToString() ?? ""; break;
        }
        c.Style.Alignment.Horizontal = v is string ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Center;
    }
}
