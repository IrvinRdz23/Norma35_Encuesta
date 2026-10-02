using Microsoft.Data.SqlClient;

namespace MahleSurvey.Data;

public interface IDbConnectionFactory
{
    SqlConnection Create();
}

public sealed class SqlConnectionFactory(IConfiguration config) : IDbConnectionFactory
{
    private readonly string _cs = config.GetConnectionString("MahleSurvey")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:MahleSurvey en la configuración.");

    public SqlConnection Create() => new(_cs);
}
