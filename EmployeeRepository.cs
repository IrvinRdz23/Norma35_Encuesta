using Dapper;
using MahleSurvey.Data;
using MahleSurvey.Models;

namespace MahleSurvey.Services;

public class EmployeeRepository(IDbConnectionFactory factory)
{
    public async Task<Employee?> FindAsync(string numeroReloj)
    {
        const string sql = @"
SELECT TOP (1)
    CAST(NumeroReloj AS nvarchar(20))  AS NumeroReloj,
    CAST(Nombre AS nvarchar(200))      AS Nombre,
    CAST(Turno AS nvarchar(100))       AS Turno,
    CAST(Puesto AS nvarchar(200))      AS Puesto,
    CAST(Planta AS nvarchar(100))      AS Planta,
    CAST(Area AS nvarchar(100))        AS Area,
    CAST(Sexo AS nvarchar(20))         AS Sexo,
    CAST(Region AS nvarchar(100))      AS Region,
    FechaNacimiento
FROM EmployeesMahle
WHERE NumeroReloj = @numeroReloj";

        await using var c = factory.Create();
        return await c.QueryFirstOrDefaultAsync<Employee>(sql, new { numeroReloj });
    }
}
