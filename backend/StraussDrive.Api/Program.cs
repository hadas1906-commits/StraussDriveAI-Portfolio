using StraussDrive.Import;
using StraussDrive.Api;
using StraussDrive.Api.Advisor;
using StraussDrive.Domain;
using Microsoft.AspNetCore.HttpOverrides;

// Container readiness checks exercise fictional rule loading and SQLite, not just a port.
if (args.FirstOrDefault() == "--healthcheck")
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.GetAsync("http://127.0.0.1:5080/api/health");
        response.EnsureSuccessStatusCode();
        using var payload = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Environment.Exit(payload.RootElement.GetProperty("status").GetString() == "healthy"
            && payload.RootElement.GetProperty("calculationRulesConfigured").GetBoolean() ? 0 : 1);
    }
    catch { Environment.Exit(1); }
    return;
}

var builder = WebApplication.CreateBuilder(args);
// Recruiter demo is local-only and never loads deployment secret files.
var localPortText = Environment.GetEnvironmentVariable("PORTFOLIO_API_PORT") ?? "5080";
if (!int.TryParse(localPortText, out var localPort) || localPort is < 1024 or > 65535)
    throw new InvalidOperationException("PORTFOLIO_API_PORT must be an unprivileged local port.");
builder.WebHost.UseUrls($"http://127.0.0.1:{localPort}");
// No cloud deployment credentials or secret-file provider are loaded in the portfolio.

// Only the default trusted loopback proxies are accepted. The VM deployment runs
// Caddy on the host and binds Kestrel to loopback; never trust arbitrary forwarded headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddSingleton<HackathonData>(_ => HackathonImporter.Load(
    builder.Configuration["HackathonData:Root"] ?? HackathonImporter.FindRoot(builder.Environment.ContentRootPath)));
builder.Services.AddSingleton(provider => provider.GetRequiredService<HackathonData>().Rules.Tax);
builder.Services.AddSingleton(provider => provider.GetRequiredService<HackathonData>().Rules.Business);
builder.Services.AddSingleton(provider => provider.GetRequiredService<HackathonData>().Advisor);
builder.Services.AddSingleton<ISalaryCalculator, SalaryCalculator>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.RespectRequiredConstructorParameters = true);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    if (context.ProblemDetails.Status == 400)
        context.ProblemDetails.Detail = "The request must be valid JSON and include all required employee, occupation and car fields. See README.md for the request format.";
});
builder.AddAdvisor();
builder.Services.AddSingleton<ScenarioLab>();
var app = builder.Build();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    ctx.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; font-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseDemoGate();
app.UseStaticFiles();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex is BadHttpRequestException badRequest ? badRequest.StatusCode : 500
});
app.UseStatusCodePages();
app.UseAdvisor();
// The imported employee browser and stateless calculators are a synthetic-demo surface.
// Fail closed when demo access is disabled, before any employee data is resolved.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    if ((path.StartsWithSegments("/api/employees") || path.StartsWithSegments("/api/vehicles")
        || path.StartsWithSegments("/api/experience") || path.StartsWithSegments("/api/calculations"))
        && !app.Configuration.GetValue("Advisor:DemoAuthEnabled", false))
    {
        await Results.Problem(statusCode: 403, title: "כניסת הדגמה כבויה.").ExecuteAsync(ctx);
        return;
    }
    await next();
});
app.MapGet("/api/health", (TaxRules tax, BusinessRules business, AdvisorStore store) => Results.Ok(new
{
    status = "healthy",
    calculationRulesConfigured = tax.Status == "configured" && business.Status == "configured"
}));
app.MapPost("/api/calculations/net", (VehicleBenefitScenario scenario, ISalaryCalculator calculator) =>
{
    try { return Results.Ok(calculator.Calculate(scenario)); }
    catch (CalculationException ex) { return Results.Problem(statusCode: 422, title: "Calculation unavailable", detail: ex.Message); }
    catch (OverflowException) { return Results.Problem(statusCode: 422, title: "סכום מחוץ לטווח החישוב הנתמך"); }
});
app.MapPost("/api/calculations/compare", (ComparisonRequest request, HackathonData data) =>
{
    if (string.IsNullOrWhiteSpace(request.EmployeeId) || !data.Employees.TryGetValue(request.EmployeeId, out var employee))
        return Results.Problem(statusCode: 422, title: "Unknown employee", detail: "Supply an imported sample employee ID.");
    try
    {
        var current = data.ResolvePortfolioSelection(data.References[employee.Id].Current);
        return Results.Ok(data.Advisor.Compare(employee, current, request.AlternativeCarId));
    }
    catch (CalculationException ex) { return Results.Problem(statusCode: 422, title: "Ineligible or incomplete benefit", detail: ex.Message); }
});
app.MapEmployeeExperience();
ScenarioLab.Map(app);
QualityDashboard.Map(app);
app.MapFallback("/api/{**path}", () => Results.NotFound());
if (Directory.Exists(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
    app.MapFallbackToFile("index.html");
app.Run();
public partial class Program { }

public sealed record ComparisonRequest(string EmployeeId, string? AlternativeCarId);
