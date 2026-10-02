namespace MahleSurvey.Models;

public class ReportFilter
{
    public string? Anexo { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

public class Totals
{
    public int Empleados { get; set; }
    public int Respuestas { get; set; }
    public DateTime? Ultima { get; set; }
}

public class Bar
{
    public string Etiqueta { get; set; } = "";
    public int Plantilla { get; set; }
    public int Encuestados { get; set; }
    public double Pct => Plantilla == 0 ? 0 : Math.Round(100.0 * Encuestados / Plantilla, 1);
}

public class DayPoint { public DateTime Dia { get; set; } public int Valor { get; set; } }
public class LabelCount { public string Etiqueta { get; set; } = ""; public int Valor { get; set; } }

public class QuestionStat
{
    public string Anexo { get; set; } = "";
    public int Numero { get; set; }
    public string? Texto { get; set; }
    public Dictionary<string, int> Conteos { get; set; } = new();
}

public class AnexoISummary
{
    public int Total { get; set; }
    public int ConEvento { get; set; }
    public double Pct => Total == 0 ? 0 : Math.Round(100.0 * ConEvento / Total, 1);
}

public class DashboardData
{
    public int Plantilla { get; set; }
    public Totals Totales { get; set; } = new();
    public double Cobertura => Plantilla == 0 ? 0 : Math.Round(100.0 * Totales.Empleados / Plantilla, 1);
    public List<Bar> PorArea { get; set; } = new();
    public List<Bar> PorTurno { get; set; } = new();
    public List<DayPoint> PorDia { get; set; } = new();
    public List<LabelCount> Distribucion { get; set; } = new();
    public List<QuestionStat> Preguntas { get; set; } = new();
    public List<LabelCount> Riesgo { get; set; } = new();
    public AnexoISummary? AnexoI { get; set; }
}

public class RawAnswer
{
    public string NumeroReloj { get; set; } = "";
    public string? Nombre { get; set; }
    public string? Area { get; set; }
    public string? Turno { get; set; }
    public string? Puesto { get; set; }
    public string Anexo { get; set; } = "";
    public string Pregunta { get; set; } = "";
    public string? Respuesta { get; set; }
    public DateTime? Fecha { get; set; }
}

public class ImportRow
{
    public string NumeroReloj { get; set; } = "";
    public int Pregunta { get; set; }
    public int Valor { get; set; }
}

public class ImportResult
{
    public int Empleados { get; set; }
    public int Filas { get; set; }
    public List<string> Advertencias { get; set; } = new();
}
