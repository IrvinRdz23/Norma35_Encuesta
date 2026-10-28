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
    private static readonly XLColor Blue = XLColor.FromHtml("#0A5A9C");
    private static readonly XLColor Zebra = XLColor.FromHtml("#F2F6FA");
    private static readonly XLColor Line = XLColor.FromHtml("#D9E1EA");
    private static readonly XLColor Muted = XLColor.FromHtml("#5D6F82");

    private const string FontName = "Segoe UI";

    /// <param name="catalog">{ código de anexo -> { número de pregunta -> texto completo } }</param>
    public byte[] Build(DashboardData d, List<RawAnswer> raw, ReportFilter f,
        IReadOnlyDictionary<string, Dictionary<int, string>> catalog)
    {
        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = FontName;
        wb.Style.Font.FontSize = 10;

        BuildSummary(wb, d, f);

        // Anexo I primero, luego Anexo III, luego cualquier otro código
        var groups = raw.GroupBy(x => x.Anexo)
            .OrderBy(g => g.Key == O.AnexoICode ? 0 : g.Key == O.AnexoIIICode ? 1 : 2)
            .ThenBy(g => g.Key);

        foreach (var g in groups)
        {
            catalog.TryGetValue(g.Key, out var texts);
            BuildAnswers(wb, g.Key, g.ToList(), texts);
        }

        BuildByQuestion(wb, d);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ------------------------------------------------------------------ Resumen

    private void BuildSummary(XLWorkbook wb, DashboardData d, ReportFilter f)
    {
        var ws = wb.AddWorksheet("Resumen");
        ws.TabColor = Navy;
        ws.ShowGridLines = false;
        ws.Column(1).Width = 3;
        ws.Column(2).Width = 42;
        for (var i = 3; i <= 5; i++) ws.Column(i).Width = 16;

        var t = ws.Cell(2, 2);
        t.Value = "MAHLE · Reporte de encuestas NOM-035";
        t.Style.Font.Bold = true; t.Style.Font.FontSize = 18; t.Style.Font.FontColor = Navy;

        var filtro = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Anexo)) filtro.Add(O.LabelFor(f.Anexo));
        if (f.Desde.HasValue) filtro.Add($"desde {f.Desde:dd/MM/yyyy}");
        if (f.Hasta.HasValue) filtro.Add($"hasta {f.Hasta:dd/MM/yyyy}");
        ws.Cell(3, 2).Value = $"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}  ·  Filtro: {(filtro.Count == 0 ? "todos los registros" : string.Join(", ", filtro))}";
        ws.Cell(3, 2).Style.Font.FontColor = Muted;

        var r = 5;
        r = Table(ws, r, "Indicadores generales", ["Indicador", "Valor"],
        [
            ["Plantilla registrada", d.Plantilla],
            ["Empleados encuestados", d.Totales.Empleados],
            ["Cobertura de la plantilla (%)", d.Cobertura],
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

        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
    }

    // ------------------------------------------------------------------ Respuestas por anexo

    private void BuildAnswers(XLWorkbook wb, string code, List<RawAnswer> rows, Dictionary<int, string>? texts)
    {
        var anexo = O.LabelFor(code);
        var ws = wb.AddWorksheet(UniqueSheetName(wb, $"Respuestas {anexo}"));
        ws.TabColor = code == O.AnexoICode ? Blue : XLColor.FromHtml("#3C8BC9");

        // Todas las preguntas del anexo, aunque nadie haya llegado a contestarlas (p. ej. las que se saltan con «No»)
        var seen = rows.Select(x => AnswerLabels.QuestionNumber(x.Pregunta))
            .Where(n => n.HasValue).Select(n => n!.Value);
        var qs = seen.Concat(texts?.Keys ?? Enumerable.Empty<int>()).Distinct().OrderBy(n => n).ToList();
        var colOf = qs.Select((n, i) => (n, i)).ToDictionary(t => t.n, t => t.i + FixedHeaders.Length + 1);

        // --- Encabezados: la pregunta completa ---
        const double qColWidth = 26;
        for (var i = 0; i < FixedHeaders.Length; i++)
            StyleHeader(ws.Cell(1, i + 1), FixedHeaders[i], Navy, XLAlignmentHorizontalValues.Left);

        var maxLines = 1;
        foreach (var n in qs)
        {
            var text = texts is not null && texts.TryGetValue(n, out var tx) ? $"{n}. {tx}" : $"Pregunta {n}";
            StyleHeader(ws.Cell(1, colOf[n]), text, Blue, XLAlignmentHorizontalValues.Left);
            maxLines = Math.Max(maxLines, (int)Math.Ceiling(text.Length / (qColWidth * 0.88)));
        }
        ws.Row(1).Height = Math.Clamp(maxLines * 14 + 14, 36, 400);

        // --- Datos: una fila por empleado ---
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
            if (max.HasValue) ws.Cell(r, 6).Value = max.Value;

            foreach (var a in emp)
            {
                var n = AnswerLabels.QuestionNumber(a.Pregunta);
                if (n is null || !colOf.TryGetValue(n.Value, out var col)) continue;
                ws.Cell(r, col).Value = AnswerLabels.Label(a.Respuesta) ?? "";
            }
            r++;
        }

        var lastRow = Math.Max(r - 1, 2);
        var lastCol = FixedHeaders.Length + Math.Max(qs.Count, 1);

        // --- Formato ---
        ws.Column(1).Width = 13;
        ws.Column(2).Width = 32;
        ws.Column(3).Width = 24;
        ws.Column(4).Width = 10;
        ws.Column(5).Width = 28;
        ws.Column(6).Width = 18;
        for (var c = FixedHeaders.Length + 1; c <= lastCol; c++) ws.Column(c).Width = qColWidth;

        var body = ws.Range(2, 1, lastRow, lastCol);
        body.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        body.Style.Border.BottomBorderColor = Line;
        body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        ws.Range(2, 4, lastRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(2, 6, lastRow, 6).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        ws.Range(2, 6, lastRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(2, 1, lastRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        if (qs.Count > 0)
        {
            var answers = ws.Range(2, FixedHeaders.Length + 1, lastRow, lastCol);
            answers.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            AddAnswerColors(answers, ws.Cell(2, FixedHeaders.Length + 1).Address.ToStringRelative());
        }

        ws.SheetView.FreezeRows(1);
        ws.SheetView.FreezeColumns(2);
        ws.Range(1, 1, lastRow, lastCol).SetAutoFilter();

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PagesWide = 1;
        ws.PageSetup.PagesTall = 0;
    }

    private static readonly string[] FixedHeaders =
        ["Núm. reloj", "Nombre", "Área", "Turno", "Puesto", "Última actualización"];

    /// <summary>Colores suaves por respuesta (formato condicional: no pesa en archivos grandes).</summary>
    private static void AddAnswerColors(IXLRange range, string firstCell)
    {
        void Rule(string answer, string bg)
        {
            var style = range.AddConditionalFormat().WhenIsTrue($"{firstCell}=\"{answer}\"");
            style.Fill.SetBackgroundColor(XLColor.FromHtml(bg));
        }
        Rule("Siempre", "#CFE2F3");
        Rule("Casi siempre", "#E4EFF9");
        Rule("Algunas veces", "#F1F1F1");
        Rule("Casi nunca", "#FCEBDD");
        Rule("Nunca", "#F8D6BF");
        Rule("Sí", "#FDE9B8");
        Rule("No", "#DDEFE5");
    }

    private static void StyleHeader(IXLCell cell, string text, XLColor fill, XLAlignmentHorizontalValues h)
    {
        cell.Value = text;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Fill.BackgroundColor = fill;
        cell.Style.Alignment.Horizontal = h;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Alignment.WrapText = true;
        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cell.Style.Border.OutsideBorderColor = XLColor.White;
    }

    // ------------------------------------------------------------------ Por pregunta

    private void BuildByQuestion(XLWorkbook wb, DashboardData d)
    {
        if (d.Preguntas.Count == 0) return;
        var ws = wb.AddWorksheet("Por pregunta");
        ws.TabColor = XLColor.FromHtml("#7DB3E0");

        string[] headers = ["Anexo", "N.º", "Pregunta", .. AnswerLabels.Order, "Total"];
        for (var i = 0; i < headers.Length; i++)
            StyleHeader(ws.Cell(1, i + 1), headers[i], Navy,
                i == 2 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Center);
        ws.Row(1).Height = 28;

        var r = 2;
        foreach (var q in d.Preguntas)
        {
            ws.Cell(r, 1).Value = O.LabelFor(q.Anexo);
            ws.Cell(r, 2).Value = q.Numero;
            ws.Cell(r, 3).Value = q.Texto ?? $"Pregunta {q.Numero}";
            for (var i = 0; i < AnswerLabels.Order.Length; i++)
            {
                var n = q.Conteos.GetValueOrDefault(AnswerLabels.Order[i]);
                if (n > 0) ws.Cell(r, 4 + i).Value = n;
            }
            ws.Cell(r, headers.Length).Value = q.Conteos.Values.Sum();
            r++;
        }

        var last = r - 1;
        ws.Column(1).Width = 14;
        ws.Column(2).Width = 7;
        ws.Column(3).Width = 80;
        for (var c = 4; c <= headers.Length; c++) ws.Column(c).Width = 14;

        var body = ws.Range(2, 1, last, headers.Length);
        body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        body.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        body.Style.Border.BottomBorderColor = Line;
        ws.Range(2, 1, last, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(2, 3, last, 3).Style.Alignment.WrapText = true;
        ws.Range(2, 4, last, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(2, headers.Length, last, headers.Length).Style.Font.Bold = true;

        ws.SheetView.FreezeRows(1);
        ws.SheetView.FreezeColumns(3);
        ws.Range(1, 1, last, headers.Length).SetAutoFilter();
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PagesWide = 1;
        ws.PageSetup.PagesTall = 0;
    }

    // ------------------------------------------------------------------ helpers

    private static string UniqueSheetName(XLWorkbook wb, string wanted)
    {
        var clean = Regex.Replace(wanted, @"[\\/*?:\[\]]", "");
        if (clean.Length > 31) clean = clean[..31];

        var name = clean;
        var i = 2;
        while (wb.Worksheets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            var suffix = $" ({i++})";
            name = clean[..Math.Min(clean.Length, 31 - suffix.Length)] + suffix;
        }
        return name;
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
        {
            var box = ws.Range(first - 1, 2, row - 1, 1 + headers.Length);
            box.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            box.Style.Border.OutsideBorderColor = Line;
        }
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
