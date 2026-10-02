namespace MahleSurvey.Options;

public class SurveyOptions
{
    /// <summary>Valor de SurveyResults.Anexo que usa hoy el formulario Anexo I (legacy: "3").</summary>
    public string AnexoICode { get; set; } = "3";
    /// <summary>Valor de SurveyResults.Anexo que usa hoy el formulario Anexo III (legacy: "1").</summary>
    public string AnexoIIICode { get; set; } = "1";
    /// <summary>Valor de SurveyResults.Anexo que usa la importación desde Excel (legacy: "3").</summary>
    public string ExcelImportCode { get; set; } = "3";
    /// <summary>Filtro SurveyQuestions.Anexo para cargar las preguntas del Anexo III (legacy: "1").</summary>
    public string QuestionsAnexoFilter { get; set; } = "1";

    public int YesValue { get; set; } = 6;
    public int NoValue { get; set; } = 7;

    /// <summary>Preguntas Sí/No que habilitan (o saltan) un bloque de preguntas dependientes.</summary>
    public List<Gate> Gates { get; set; } = new();

    public int ExcelEmployeeColumn { get; set; } = 3;
    public int ExcelFirstQuestionColumn { get; set; } = 10;
    public int ExcelLastQuestionColumn { get; set; } = 81;

    public RiskThresholds Risk { get; set; } = new();
    public Dictionary<string, string> AnexoLabels { get; set; } = new();

    public string LabelFor(string code) =>
        AnexoLabels.TryGetValue(code, out var l) ? l : $"Anexo (código {code})";
}

public class Gate
{
    public int Question { get; set; }
    public List<int> Dependents { get; set; } = new();
}

/// <summary>Cortes de la calificación final de la Guía III (NOM-035). Verificar contra la norma vigente.</summary>
public class RiskThresholds
{
    public int Bajo { get; set; } = 20;
    public int Medio { get; set; } = 45;
    public int Alto { get; set; } = 70;
    public int MuyAlto { get; set; } = 90;
}
