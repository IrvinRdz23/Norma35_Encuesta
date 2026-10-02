using Dapper;
using Microsoft.Extensions.Options;
using MahleSurvey.Data;
using MahleSurvey.Models;
using MahleSurvey.Options;

namespace MahleSurvey.Services;

public class ReportRepository(IDbConnectionFactory factory, IOptions<SurveyOptions> options, SurveyRepository surveys)
{
    private SurveyOptions O => options.Value;

    // Compatible con SQL Server < 2012 (sin TRY_CONVERT)
    private const string Fecha =
        "(CASE WHEN ISDATE(r.FechaActualizacion) = 1 THEN CONVERT(datetime, r.FechaActualizacion, 121) END)";

    private static string Where(bool withAnexo = true) =>
        "WHERE 1 = 1 " +
        (withAnexo ? "AND (@anexo IS NULL OR r.Anexo = @anexo) " : "") +
        $"AND (@desde IS NULL OR {Fecha} >= @desde) " +
        $"AND (@hasta IS NULL OR {Fecha} < DATEADD(day, 1, @hasta))";

    public async Task<List<string>> GetAnexosAsync()
    {
        await using var c = factory.Create();
        var list = await c.QueryAsync<string>("SELECT DISTINCT CAST(Anexo AS nvarchar(20)) FROM SurveyResults ORDER BY 1");
        return list.ToList();
    }

    public async Task<DashboardData> GetDashboardAsync(ReportFilter f)
    {
        var anexo = string.IsNullOrWhiteSpace(f.Anexo) ? null : f.Anexo;
        var p = new { anexo, desde = f.Desde, hasta = f.Hasta };
        var w = Where();
        var d = new DashboardData();

        await using var c = factory.Create();
        await c.OpenAsync();

        d.Totales = await c.QuerySingleAsync<Totals>(
            $@"SELECT COUNT(DISTINCT r.NumeroReloj) AS Empleados, COUNT(*) AS Respuestas, MAX({Fecha}) AS Ultima
               FROM SurveyResults r {w}", p);

        d.Plantilla = await c.ExecuteScalarAsync<int>("SELECT COUNT(DISTINCT NumeroReloj) FROM EmployeesMahle");
        d.PorArea = await BarsAsync(c, "Area", p, w);
        d.PorTurno = await BarsAsync(c, "Turno", p, w);

        d.PorDia = (await c.QueryAsync<DayPoint>(
            $@"SELECT CAST({Fecha} AS date) AS Dia, COUNT(DISTINCT r.NumeroReloj) AS Valor
               FROM SurveyResults r {w} AND {Fecha} IS NOT NULL
               GROUP BY CAST({Fecha} AS date) ORDER BY 1", p)).ToList();

        // Distribución por pregunta/respuesta, normalizada a etiquetas legibles
        var raw = await c.QueryAsync<(string Anexo, string Pregunta, string? Respuesta, int N)>(
            $@"SELECT CAST(r.Anexo AS nvarchar(20)) AS Anexo,
                      CAST(r.IdentificadorPregunta AS nvarchar(50)) AS Pregunta,
                      CAST(r.IdentificadorRespuesta AS nvarchar(100)) AS Respuesta,
                      COUNT(*) AS N
               FROM SurveyResults r {w}
               GROUP BY r.Anexo, r.IdentificadorPregunta, r.IdentificadorRespuesta", p);

        var texts = await surveys.GetQuestionTextsAsync();
        var global = new Dictionary<string, int>();
        var perQ = new Dictionary<(string, int), QuestionStat>();

        foreach (var (an, preg, resp, n) in raw)
        {
            var label = AnswerLabels.Label(resp);
            var num = AnswerLabels.QuestionNumber(preg);
            if (label is null || num is null) continue;

            global[label] = global.GetValueOrDefault(label) + n;

            if (!perQ.TryGetValue((an, num.Value), out var qs))
            {
                var texto = an == O.AnexoICode && an != O.ExcelImportCode
                    ? AnexoIDefinition.All.FirstOrDefault(x => x.Id == num.Value)?.Text
                    : texts.GetValueOrDefault(num.Value);
                qs = new QuestionStat { Anexo = an, Numero = num.Value, Texto = texto };
                perQ[(an, num.Value)] = qs;
            }
            qs.Conteos[label] = qs.Conteos.GetValueOrDefault(label) + n;
        }

        d.Distribucion = AnswerLabels.Order.Where(global.ContainsKey)
            .Select(l => new LabelCount { Etiqueta = l, Valor = global[l] }).ToList();
        d.Preguntas = perQ.Values.OrderBy(x => x.Anexo).ThenBy(x => x.Numero).ToList();

        // Niveles de riesgo (Anexo III): deshabilitado hasta confirmar de dónde salen los puntos
        // (no existen ResponseValues ni SurveyResults.ValorRespuesta en esta BD). d.Riesgo queda vacío
        // y la gráfica y la hoja de Excel correspondientes se ocultan solas.

        // Anexo I: sólo agregados (dato sensible, no se listan personas)
        var wd = Where(false);
        var qList = string.Join(",", Enumerable.Range(1, 6).Select(i => $"'question[{i}]'"));
        var a1 = await c.QuerySingleAsync<AnexoISummary>(
            $@"SELECT COUNT(DISTINCT CASE WHEN r.IdentificadorPregunta = 'question[1]'
                        AND CAST(r.IdentificadorRespuesta AS nvarchar(20)) IN ('6','7') THEN r.NumeroReloj END) AS Total,
                      COUNT(DISTINCT CASE WHEN r.IdentificadorPregunta IN ({qList})
                        AND CAST(r.IdentificadorRespuesta AS nvarchar(20)) = '6' THEN r.NumeroReloj END) AS ConEvento
               FROM SurveyResults r {wd} AND r.Anexo = @code",
            new { desde = f.Desde, hasta = f.Hasta, code = O.AnexoICode });
        d.AnexoI = a1.Total > 0 ? a1 : null;

        return d;
    }

    private static async Task<List<Bar>> BarsAsync(Microsoft.Data.SqlClient.SqlConnection c, string col, object p, string w)
    {
        // col viene de una lista fija en este archivo ("Area" | "Turno"), nunca del usuario.
        var sql = $@"
;WITH s AS (
    SELECT DISTINCT CAST(r.NumeroReloj AS nvarchar(20)) AS NumeroReloj FROM SurveyResults r {w}
),
e AS (
    SELECT CAST(NumeroReloj AS nvarchar(20)) AS NumeroReloj,
           ISNULL(NULLIF(LTRIM(RTRIM(CAST(MAX({col}) AS nvarchar(100)))), ''), 'Sin dato') AS Etiqueta
    FROM EmployeesMahle GROUP BY NumeroReloj
)
SELECT e.Etiqueta, COUNT(*) AS Plantilla, COUNT(s.NumeroReloj) AS Encuestados
FROM e LEFT JOIN s ON s.NumeroReloj = e.NumeroReloj
GROUP BY e.Etiqueta
ORDER BY COUNT(s.NumeroReloj) DESC, e.Etiqueta";
        return (await c.QueryAsync<Bar>(sql, p)).ToList();
    }

    public async Task<List<RawAnswer>> GetRawAsync(ReportFilter f)
    {
        var anexo = string.IsNullOrWhiteSpace(f.Anexo) ? null : f.Anexo;
        var sql = $@"
;WITH e AS (
    SELECT CAST(NumeroReloj AS nvarchar(20)) AS NumeroReloj,
           MAX(CAST(Nombre AS nvarchar(200))) AS Nombre,
           MAX(CAST(Area AS nvarchar(100))) AS Area,
           MAX(CAST(Turno AS nvarchar(100))) AS Turno,
           MAX(CAST(Puesto AS nvarchar(200))) AS Puesto
    FROM EmployeesMahle GROUP BY NumeroReloj
)
SELECT CAST(r.NumeroReloj AS nvarchar(20)) AS NumeroReloj, e.Nombre, e.Area, e.Turno, e.Puesto,
       CAST(r.Anexo AS nvarchar(20)) AS Anexo,
       CAST(r.IdentificadorPregunta AS nvarchar(50)) AS Pregunta,
       CAST(r.IdentificadorRespuesta AS nvarchar(100)) AS Respuesta,
       {Fecha} AS Fecha
FROM SurveyResults r
LEFT JOIN e ON e.NumeroReloj = CAST(r.NumeroReloj AS nvarchar(20))
{Where()}";

        await using var c = factory.Create();
        return (await c.QueryAsync<RawAnswer>(sql, new { anexo, desde = f.Desde, hasta = f.Hasta }, commandTimeout: 300)).ToList();
    }
}
