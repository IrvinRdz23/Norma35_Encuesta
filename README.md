# MahleSurvey (.NET 8 MVC)

Migración de MAHLESurvey (WebForms / .NET Framework) a ASP.NET Core 8 MVC + Razor.

## Puesta en marcha
1. Copie sus imágenes a `wwwroot/images/` (`MahleMDWB.png`, `MahleIcoSH.ico`).
2. Restaure librerías de front-end: `dotnet tool install -g Microsoft.Web.LibraryManager.Cli` y luego `libman restore`
   (Visual Studio lo hace solo al abrir el proyecto).
3. Contraseña de BD fuera del código:
   `dotnet user-secrets init` y
   `dotnet user-secrets set "ConnectionStrings:MahleSurvey" "Server=...;Database=MAHLESurvey;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True"`
   En producción use variable de entorno `ConnectionStrings__MahleSurvey`.
4. `dotnet run`
5. Hosting en IIS: instalar el *.NET 8 Hosting Bundle*, publicar con `dotnet publish -c Release`.

## Configuración (appsettings.json → Survey)
Códigos de `SurveyResults.Anexo`, preguntas Sí/No con dependientes, columnas del Excel y cortes de riesgo.
