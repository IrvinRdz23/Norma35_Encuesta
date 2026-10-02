namespace MahleSurvey.Models;

public record AnswerOption(string Text, int Value);

public class SurveyQuestion
{
    public int Id { get; set; }
    public string Texto { get; set; } = "";
    public List<AnswerOption> Options { get; set; } = new();
}

public abstract class SurveyViewModel
{
    public string? NumeroReloj { get; set; }
    public Employee? Employee { get; set; }
    public Dictionary<int, int> Answers { get; set; } = new();
    public HashSet<int> Missing { get; set; } = new();
}

public class AnexoIViewModel : SurveyViewModel { }

public class AnexoIIIViewModel : SurveyViewModel
{
    public List<SurveyQuestion> Questions { get; set; } = new();
    /// <summary>pregunta dependiente -> pregunta Sí/No que la habilita</summary>
    public Dictionary<int, int> DependentOf { get; set; } = new();
}

public static class AnexoIDefinition
{
    public record Q(int Id, string Text);
    public record Section(string Title, string? Intro, bool Conditional, IReadOnlyList<Q> Questions);

    public static readonly int[] GateIds = [1, 2, 3, 4, 5, 6];

    public static readonly IReadOnlyList<Section> Sections =
    [
        new("I. Acontecimiento traumático severo",
            "¿Ha presenciado o sufrido alguna vez, durante o con motivo del trabajo, un acontecimiento como los siguientes?",
            false,
            [
                new(1, "Accidente que tenga como consecuencia la muerte, la pérdida de un miembro o una lesión grave"),
                new(2, "Asaltos"),
                new(3, "Actos violentos que derivaron en lesiones graves"),
                new(4, "Secuestro"),
                new(5, "Amenazas"),
                new(6, "Cualquier otro que ponga en riesgo su vida o salud, y/o la de otras personas"),
            ]),
        new("II. Recuerdos persistentes sobre el acontecimiento (durante el último mes)", null, true,
            [
                new(7, "¿Ha tenido recuerdos recurrentes sobre el acontecimiento que le provocan malestares?"),
                new(8, "¿Ha tenido sueños de carácter recurrente sobre el acontecimiento, que le producen malestar?"),
            ]),
        new("III. Esfuerzo por evitar circunstancias parecidas o asociadas al acontecimiento (durante el último mes)", null, true,
            [
                new(9, "¿Se ha esforzado por evitar todo tipo de sentimientos, conversaciones o situaciones que le puedan recordar el acontecimiento?"),
                new(10, "¿Se ha esforzado por evitar todo tipo de actividades, lugares o personas que motivan recuerdos del acontecimiento?"),
                new(11, "¿Ha tenido dificultad para recordar alguna parte importante del evento?"),
                new(12, "¿Ha disminuido su interés en sus actividades cotidianas?"),
                new(13, "¿Se ha sentido usted alejado o distante de los demás?"),
                new(14, "¿Ha notado que tiene dificultad para expresar sus sentimientos?"),
                new(15, "¿Ha tenido la impresión de que su vida se va a acortar, que va a morir antes que otras personas o que tiene un futuro limitado?"),
            ]),
        new("IV. Afectación (durante el último mes)", null, true,
            [
                new(16, "¿Ha tenido usted dificultades para dormir?"),
                new(17, "¿Ha estado particularmente irritable o le han dado arranques de coraje?"),
                new(18, "¿Ha tenido dificultad para concentrarse?"),
                new(19, "¿Ha estado nervioso o constantemente en alerta?"),
                new(20, "¿Se ha sobresaltado fácilmente por cualquier cosa?"),
            ]),
    ];

    public static IEnumerable<Q> All => Sections.SelectMany(s => s.Questions);
}
