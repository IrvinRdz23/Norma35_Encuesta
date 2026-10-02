using MahleSurvey.Data;
using MahleSurvey.Options;
using MahleSurvey.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<SurveyOptions>(builder.Configuration.GetSection("Survey"));
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<EmployeeRepository>();
builder.Services.AddScoped<SurveyRepository>();
builder.Services.AddScoped<ReportRepository>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddSingleton<ExcelExportService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
