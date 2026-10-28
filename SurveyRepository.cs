using Dapper;
using Microsoft.Extensions.Options;
using MahleSurvey.Data;
using MahleSurvey.Models;
using MahleSurvey.Options;

namespace MahleSurvey.Services;

public class SurveyRepository(IDbConnectionFactory factory, IOptions<SurveyOptions> options)
{
    private SurveyOptions O => options.Value;

    private sealed class QRow
    {
        public int Id { get; set; }
        public string? Pregunta { get; set; }
    }

    private async Task<List<QRow>> LoadQuestionRowsAsync(string anexo)
    {
        const string sql = @"
SELECT q.id AS Id, CAST(q.Pregunta AS nvarchar(max)) AS Pregunta
FROM SurveyQuestions q
WHERE CAST(q.Anexo AS nvarchar(20)) = @anexo
ORDER BY q.id";

        await using var c = factory.Create();
        return (await c.QueryAsync<QRow>(sql, new { anexo })).ToList();
    }

    /// <summary>Preguntas del Anexo III. Busca primero con QuestionsAnexoFilter y, si no hay, con AnexoIIICode.</summary>
    private async Task<List<QRow>> LoadAnexoIIIRowsAsync()
    {
        var rows = await LoadQuestionRowsAsync(O.QuestionsAnexoFilter);
        if (rows.Count == 0 && O.QuestionsAnexoFilter != O.AnexoIIICode)
            rows = await LoadQuestionRowsAsync(O.AnexoIIICode);
        return rows;
    }

    /// <summary>
    /// Preguntas del Anexo III con sus opciones. Ya no depende de la tabla ResponseValues:
    /// las opciones son la escala estándar y las preguntas Sí/No son las configuradas en Survey:Gates.
    /// </summary>
    public async Task<List<SurveyQuestion>> GetQuestionsAsync()
    {
        var rows = await LoadAnexoIIIRowsAsync();
        var yesNo = O.Gates.Select(g => g.Question).ToHashSet();

        return rows.GroupBy(r => r.Id).Select(g =>
        {
            var r = g.First();
            var q = new SurveyQuestion { Id = r.Id, Texto = r.Pregunta ?? $"Pregunta {r.Id}" };
            if (yesNo.Contains(r.Id))
            {
                q.Options.Add(new("Sí", O.YesValue));
                q.Options.Add(new("No", O.NoValue));
            }
            else
            {
                q.Options.Add(new("Siempre", 1));
                q.Options.Add(new("Casi siempre", 2));
                q.Options.Add(new("Algunas veces", 3));
                q.Options.Add(new("Casi nunca", 4));
                q.Options.Add(new("Nunca", 5));
            }
            return q;
        }).ToList();
    }

    /// <summary>
    /// Texto de cada pregunta por código de anexo (SurveyResults.Anexo): { código -> { número -> texto } }.
    /// Anexo I sale de la definición fija; Anexo III (y la importación de Excel) de SurveyQuestions.
    /// Si SurveyQuestions no responde, esos encabezados caen a "Pregunta n".
    /// </summary>
    public async Task<Dictionary<string, Dictionary<int, string>>> GetQuestionCatalogAsync()
    {
        var catalog = new Dictionary<string, Dictionary<int, string>>
        {
            [O.AnexoICode] = AnexoIDefinition.All.ToDictionary(q => q.Id, q => q.Text)
        };

        var a3 = new Dictionary<int, string>();
        try
        {
            foreach (var r in await LoadAnexoIIIRowsAsync())
                if (!string.IsNullOrWhiteSpace(r.Pregunta)) a3.TryAdd(r.Id, r.Pregunta.Trim());
        }
        catch { /* sin catálogo: los encabezados usan "Pregunta n" */ }

        catalog.TryAdd(O.AnexoIIICode, a3);
        catalog.TryAdd(O.ExcelImportCode, a3);
        return catalog;
    }

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private static Task EnsureEmployeeAsync(Microsoft.Data.SqlClient.SqlConnection c, System.Data.Common.DbTransaction tx, Employee e, string fecha) =>
        c.ExecuteAsync(@"
IF NOT EXISTS (SELECT 1 FROM SurveyEmployees WHERE NumeroReloj = @NumeroReloj)
INSERT INTO SurveyEmployees (NumeroReloj, Nombre, Turno, Puesto, Planta, Area, Sexo, FechaNacimiento, FechaActualizacion, Region)
VALUES (@NumeroReloj, @Nombre, @Turno, @Puesto, @Planta, @Area, @Sexo, @FechaNacimiento, @Fecha, @Region)",
        new { e.NumeroReloj, e.Nombre, e.Turno, e.Puesto, e.Planta, e.Area, e.Sexo, e.FechaNacimiento, Fecha = fecha, e.Region }, tx);

    /// <summary>Anexo I: guarda "question[n]" + "6"/"7" bajo AnexoICode.</summary>
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

    /// <summary>Anexo III: guarda id de pregunta + texto de la respuesta bajo AnexoIIICode.</summary>
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
            n = emp.NumeroReloj, p = a.QuestionId.ToString(), r = a.Text, f = fecha, a = O.AnexoIIICode
        }).ToList();

        await c.ExecuteAsync(@"INSERT INTO SurveyResults (NumeroReloj, IdentificadorPregunta, IdentificadorRespuesta, FechaActualizacion, Anexo)
                               VALUES (@n, @p, @r, @f, @a)", rows, tx);
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
