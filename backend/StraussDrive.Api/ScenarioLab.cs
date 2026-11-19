using System.Security.Claims;
using System.Text.Json.Serialization;
using StraussDrive.Api.Advisor;
using StraussDrive.Domain;
using StraussDrive.Import;

namespace StraussDrive.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LabScenarioRequest(string Name, string? AlternativeCarId, SalaryInputs Inputs);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LabPreviewRequest(LabScenarioRequest[] Scenarios);
public sealed record SavedScenario(string Id, string OwnerId, string ProfileKey, string Name, LabScenarioRequest Request, string SourceFingerprint, string CreatedAt);
public sealed record LabScenarioResult(string? Id, string Name, string Status, string NextStep, string? CreatedAt, LabScenarioRequest Request, ComparisonDto? Comparison);

public sealed class ScenarioLab(HackathonData data)
{
    public SampleEmployee Profile(EmployeeSession session) => data.Employees.TryGetValue(session.ProfileKey, out var employee) ? employee
        : throw new AdvisorException(422, "מעבדת התרחישים דורשת פרופיל הדגמה מיובא. בחרו עובד הדגמה פעיל כדי לקבל בסיס מאומת.");
    public LabScenarioResult Evaluate(EmployeeSession session, LabScenarioRequest request, SavedScenario? saved = null)
    {
        var original = Profile(session);
        if (request is null || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80 || request.Inputs is null)
            throw new AdvisorException(422, "יש להזין שם תרחיש עד 80 תווים ואת כל רכיבי השכר, כולל אפסים מפורשים.");
        var inputs = request.Inputs;
        AdvisorTools.Validate(new(BaseSalary: inputs.BaseSalary, Commission: inputs.Commission, Bonus: inputs.Bonus,
            PensionPercent: inputs.PensionPercent, StudyFundPercent: inputs.StudyFundPercent, CreditPoints: inputs.CreditPoints));
        LabScenarioResult Result(string status, string next, ComparisonDto? comparison = null) => new(saved?.Id, request.Name.Trim(), status, next, saved?.CreatedAt, request, comparison);
        if (saved is not null && saved.SourceFingerprint != DecisionEvidenceBuilder.Fingerprint(data))
            return Result("stale_source", "מקורות הכללים השתנו מאז השמירה. צרו תרחיש חדש לפי המקור הנוכחי; התוצאה הישנה לא הוצגה כחישוב עדכני.");
        if (request.AlternativeCarId is not null && !data.Catalog.ContainsKey(request.AlternativeCarId))
            throw new AdvisorException(422, "יש לבחור רכב מהקטלוג הפיקטיבי או ויתור מפורש.");
        var hypothetical = original with { BaseSalary = inputs.BaseSalary, Commission = inputs.Commission, Bonus = inputs.Bonus,
            PensionRate = inputs.PensionPercent / 100m, StudyFundRate = inputs.StudyFundPercent / 100m, CreditPoints = inputs.CreditPoints };
        var eligibility = data.Advisor.CheckEligibility(hypothetical, request.AlternativeCarId);
        if (!eligibility.Eligible) return Result("policy_blocked", request.AlternativeCarId is null
            ? "העיסוק המיובא מחייב רכב. בחרו חלופת רכב; שינוי שכר אינו מעניק זכאות לויתור."
            : "החלופה אינה במסגרת הזכאות המיובאת או תנאי המשפחה. בחרו רכב מתאים מהקטלוג; טענה בשיחה לא משנה זכאות.");
        var currentId = data.ResolvePortfolioSelection(data.References[original.Id].Current);
        var sameRoute = currentId == request.AlternativeCarId;
        var comparison = new SalaryComparison(data.Advisor.Calculate(original, currentId, original.CurrentParticipation), data.Advisor.Calculate(hypothetical, request.AlternativeCarId, sameRoute ? original.CurrentParticipation : null));
        return Result("calculated", "ההשוואה היא מול המצב המיובא המקורי. התרחיש לא שינה את השכר, העיסוק, הזכאות או הפניות הקיימות.",
            EmployeeExperience.Present("owned-scenario", comparison, data, "hypothetical", sameRoute ? "imported_current" : "catalog_default"));
    }
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/advisor/scenarios").RequireAuthorization("Employee");
        EmployeeSession Session(HttpContext ctx, AdvisorStore store)
        {
            var session = store.Session(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!) ?? throw new AdvisorException(401, "יש להתחבר מחדש.");
            if (ctx.Request.Headers.TryGetValue("X-Strauss-Profile", out var expected) && expected != session.ProfileKey)
                throw new AdvisorException(409, "הפרופיל הפעיל השתנה בחלון אחר. רעננו לפני השוואה או שמירה.");
            return session;
        }
        group.MapGet("/", (HttpContext ctx, AdvisorStore store, ScenarioLab lab) => {
            var session = Session(ctx, store); lab.Profile(session);
            return Results.Ok(store.Scenarios(session.Id).Select(s => lab.Evaluate(session, s.Request, s)).ToArray());
        });
        group.MapPost("/preview", (LabPreviewRequest request, HttpContext ctx, AdvisorStore store, ScenarioLab lab) => {
            if (request.Scenarios is null || request.Scenarios.Length is < 1 or > 4) throw new AdvisorException(422, "אפשר להשוות בין תרחיש אחד לארבעה בכל פעם.");
            var session = Session(ctx, store);
            return Results.Ok(request.Scenarios.Select(s => lab.Evaluate(session, s)).ToArray());
        }).RequireRateLimiting("AdvisorTurns");
        group.MapPost("/", (LabScenarioRequest request, HttpContext ctx, AdvisorStore store, ScenarioLab lab, HackathonData data) => {
            var session = Session(ctx, store); var result = lab.Evaluate(session, request);
            if (result.Comparison is null) throw new AdvisorException(422, result.NextStep);
            var saved = store.SaveScenario(session, request, DecisionEvidenceBuilder.Fingerprint(data));
            return Results.Ok(lab.Evaluate(session, request, saved));
        }).RequireRateLimiting("AdvisorTurns");
        group.MapPost("/{id}/remove", (string id, HttpContext ctx, AdvisorStore store) => {
            store.RemoveScenario(id, Session(ctx, store).Id); return Results.Ok(new { removed = true });
        }).RequireRateLimiting("AdvisorTurns");
    }
}
