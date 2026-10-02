using Dapper;
using Microsoft.Extensions.Options;
using MahleSurvey.Data;
using MahleSurvey.Models;
using MahleSurvey.Options;

namespace MahleSurvey.Services;

public class SurveyRepository(IDbConnectionFactory factory, IOptions<SurveyOptions> options)
{
    private SurveyOptions O => options.Value;

    public class QuestionRow
    {
        public int Id { get; set; }
        public string Pregunta { get; set; } = "";
        public string? Identificador { get; set; }
        public int? ValorSiempre { get; set; }
        public int? ValorCasiSiempre { get; set; }
        public int? ValorAlgunasVeces { get; set; }
        public int? ValorCasiNunca { get; set; }
        public int? ValorNunca { get; set; }
    }

    public async Task<List<SurveyQuestion>> GetQuestionsAsync()
    {
        const string sql = @"
SELECT q.id AS Id, q.Pregunta, q.Identificador,
       rv.ValorSiempre, rv.ValorCasiSiempre, rv.ValorAlgunasVeces, rv.ValorCasiNunca, rv.ValorNunca
FROM SurveyQuestions q
LEFT JOIN ResponseValues rv ON q.Identificador = rv.IdentificadorPregunta
WHERE q.Anexo = @Anexo
ORDER BY q.id";

        await using var c = factory.Create();
        var rows = await c.QueryAsync<QuestionRow>(sql, new { Anexo = O.QuestionsAnexoFilter });
        var yesNo = O.Gates.Select(g => g.Question).ToHashSet();

        return rows.GroupBy(r => r.Id).Select(g =>
        {
            var r = g.First();
            var q = new SurveyQuestion { Id = r.Id, Texto = r.Pregunta };
            if (yesNo.Contains(r.Id))
            {
                q.Options.Add(new("Sí", O.YesValue));
                q.Options.Add(new("No", O.NoValue));
            }
            else
            {
                void Add(string t, int? v) { if (v.HasValue) q.Options.Add(new(t, v.Value)); }
                Add("Siempre", r.ValorSiempre);
                Add("Casi siempre", r.ValorCasiSiempre);
                Add("Algunas veces", r.ValorAlgunasVeces);
                Add("Casi nunca", r.ValorCasiNunca);
                Add("Nunca", r.ValorNunca);
            }
            return q;
        }).ToList();
    }

    public async Task<Dictionary<int, string>> GetQuestionTextsAsync()
    {
        await using var c = factory.Create();
        var rows = await c.QueryAsync<(int Id, string Pregunta)>(
            "SELECT id AS Id, Pregunta FROM SurveyQuestions WHERE Anexo = @Anexo",
            new { Anexo = O.QuestionsAnexoFilter });
        return rows.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First().Pregunta);
    }

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private static Task EnsureEmployeeAsync(Microsoft.Data.SqlClient.SqlConnection c, System.Data.Common.DbTransaction tx, Employee e, string fecha) =>
        c.ExecuteAsync(@"
IF NOT EXISTS (SELECT 1 FROM SurveyEmployees WHERE NumeroReloj = @NumeroReloj)
INSERT INTO SurveyEmployees (NumeroReloj, Nombre, Turno, Puesto, Planta, Area, Sexo, FechaNacimiento, FechaActualizacion, Region)
VALUES (@NumeroReloj, @Nombre, @Turno, @Puesto, @Planta, @Area, @Sexo, @FechaNacimiento, @Fecha, @Region)",
        new { e.NumeroReloj, e.Nombre, e.Turno, e.Puesto, e.Planta, e.Area, e.Sexo, e.FechaNacimiento, Fecha = fecha, e.Region }, tx);

    /// <summary>Anexo I: guarda "question[n]" + "6"/"7" bajo AnexoICode (mismo formato que el sistema anterior).</summary>
    public async Task SaveAnexoIAsync(Employee emp, IEnumerable<KeyValuePair<int, int>> answers)
    {
        await using var c = factory.Create();
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        var fecha = Now();

        await EnsureEmployeeAsync(c, tx, emp, fecha);
        await c.ExecuteAsync("DELETE FROM SurveyResults WHERE NumeroReloj = @n AND Anexo = @a",
            new { n = emp.NumeroReloj, a = O.AnexoICode }, tx);

        var rows = answers.Select(a => new
        {
            n = emp.NumeroReloj, p = $"question[{a.Key}]", r = a.Value.ToString(), f = fecha, a = O.AnexoICode
        }).ToList();

        await c.ExecuteAsync(@"INSERT INTO SurveyResults (NumeroReloj, IdentificadorPregunta, IdentificadorRespuesta, FechaActualizacion, Anexo)
                               VALUES (@n, @p, @r, @f, @a)", rows, tx);
        await tx.CommitAsync();
    }

    /// <summary>Anexo III: guarda id de pregunta + texto de respuesta + ValorRespuesta bajo AnexoIIICode.</summary>
    public async Task SaveAnexoIIIAsync(Employee emp, IEnumerable<(int QuestionId, string Text, int Value)> answers)
    {
        await using var c = factory.Create();
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        var fecha = Now();

        await EnsureEmployeeAsync(c, tx, emp, fecha);
        await c.ExecuteAsync("DELETE FROM SurveyResults WHERE NumeroReloj = @n AND Anexo = @a",
            new { n = emp.NumeroReloj, a = O.AnexoIIICode }, tx);

        var rows = answers.Select(a => new
        {
            n = emp.NumeroReloj, p = a.QuestionId.ToString(), r = a.Text, f = fecha, a = O.AnexoIIICode, v = a.Value
        }).ToList();

        await c.ExecuteAsync(@"INSERT INTO SurveyResults (NumeroReloj, IdentificadorPregunta, IdentificadorRespuesta, FechaActualizacion, Anexo, ValorRespuesta)
                               VALUES (@n, @p, @r, @f, @a, @v)", rows, tx);
        await tx.CommitAsync();
    }

    /// <summary>Importación Excel: reemplaza las respuestas "question[n]" de los empleados incluidos en el archivo.</summary>
    public async Task ReplaceImportedAsync(IReadOnlyCollection<ImportRow> rows)
    {
        if (rows.Count == 0) return;
        await using var c = factory.Create();
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        var fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var anexo = O.ExcelImportCode;

        foreach (var chunk in rows.Select(r => r.NumeroReloj).Distinct().Chunk(1000))
        {
            await c.ExecuteAsync(
                "DELETE FROM SurveyResults WHERE Anexo = @anexo AND NumeroReloj IN @ids AND IdentificadorPregunta LIKE 'question[[]%'",
                new { anexo, ids = chunk }, tx, commandTimeout: 300);
        }

        var inserts = rows.Select(r => new
        {
            n = r.NumeroReloj, p = $"question[{r.Pregunta}]", r = r.Valor, f = fecha, a = anexo
        }).ToList();

        await c.ExecuteAsync(@"INSERT INTO SurveyResults (NumeroReloj, IdentificadorPregunta, IdentificadorRespuesta, FechaActualizacion, Anexo)
                               VALUES (@n, @p, @r, @f, @a)", inserts, tx, commandTimeout: 600);
        await tx.CommitAsync();
    }
}
