namespace MahleSurvey.Models;

public class Employee
{
    public string NumeroReloj { get; set; } = "";
    public string? Nombre { get; set; }
    public string? Turno { get; set; }
    public string? Puesto { get; set; }
    public string? Planta { get; set; }
    public string? Area { get; set; }
    public string? Sexo { get; set; }
    public string? Region { get; set; }
    public object? FechaNacimiento { get; set; }

    public string FechaTexto => FechaNacimiento switch
    {
        null => "",
        DateTime d => d.ToString("dd/MM/yyyy"),
        var o => o.ToString() ?? ""
    };
}
