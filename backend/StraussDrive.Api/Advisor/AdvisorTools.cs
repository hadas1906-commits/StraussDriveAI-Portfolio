using StraussDrive.Domain;
using StraussDrive.Import;
namespace StraussDrive.Api.Advisor;

public sealed record VehicleLookup(string Query);
public sealed record EligibilityCheck(EmployeeFacts Facts, string Alternative);
public sealed record NetCalculation(EmployeeFacts Facts, string Choice, decimal? Participation = null);
public sealed record ScenarioComparison(EmployeeFacts Facts, string Alternative, decimal? Participation = null);
public sealed record RepresentativeEscalation(string ConversationId, string Category, string? VehicleId, EmployeeFacts Facts, bool ForCurrent = false);
public interface IAdvisorTools
{
    CatalogVehicle[] LookupVehicle(VehicleLookup request);
    BenefitEligibility CheckEligibility(EligibilityCheck request);
    BenefitCalculation CalculateNet(NetCalculation request);
    SalaryComparison CompareScenarios(ScenarioComparison request);
    RepresentativeTicket EscalateToRepresentative(RepresentativeEscalation request);
}
public sealed class AdvisorTools(HackathonData data) : IAdvisorTools
{
    public CatalogVehicle[] LookupVehicle(VehicleLookup request)
    {
        return VehicleSearch.Lookup(data.Catalog.Values, request.Query);
    }
    public SampleEmployee Employee(EmployeeFacts f, bool family = false)
    {
        Validate(f);
        var occupation = f.Occupation is { } o && data.Occupations.TryGetValue(o, out var policy) ? policy : throw new CalculationException("יש לבחור עיסוק מהטבלה הפיקטיבית.");
        // Child count remains unknown in state for ordinary routes; it is never used to authorize a family car without an explicit count.
        var children = f.EligibleChildren ?? (family ? throw new CalculationException("חסר מספר ילדים עד גיל הזכאות.") : 0);
        return new("private-session", "הפרופיל שלך", occupation.Name, occupation.Group,
            f.BaseSalary ?? throw Missing("שכר בסיס"), f.Commission ?? throw Missing("עמלות, גם אם אפס"), f.Bonus ?? throw Missing("בונוס, גם אם אפס"),
            (f.PensionPercent ?? throw Missing("אחוז פנסיה")) / 100m, (f.StudyFundPercent ?? throw Missing("אחוז קרן השתלמות")) / 100m,
            f.CreditPoints ?? throw Missing("נקודות זיכוי"), children, f.CurrentCar ?? throw Missing("מסלול נוכחי"),
            f.CurrentCar == "waiver" ? 0 : f.CurrentParticipation ?? throw Missing("השתתפות נוכחית"), new Dictionary<string, string>());
    }
    private static CalculationException Missing(string value) => new($"חסר מידע: {value}.");
    public static void Validate(EmployeeFacts f)
    {
        if (f.BaseSalary is < 0 or > 100000000 || f.Commission is < 0 or > 100000000 || f.Bonus is < 0 or > 100000000 ||
            f.PensionPercent is < 0 or > 100 || f.StudyFundPercent is < 0 or > 100 || f.CreditPoints is < 0 or > 100 ||
            f.CurrentParticipation is < 0 or > 1000000 || f.EligibleChildren is < 0 or > 100)
            throw new AdvisorException(422, "יש להזין סכומים לא שליליים, אחוזים בין 0 ל־100 ונתונים בטווח הנתמך.");
    }
    public BenefitEligibility CheckEligibility(EligibilityCheck request)
    {
        Validate(request.Facts);
        var f = request.Facts;
        if (f.Occupation is null || !data.Occupations.TryGetValue(f.Occupation, out var o)) throw new CalculationException("חסר עיסוק תקין.");
        var family = data.Rules.Policy.Family.Cars.Contains(request.Alternative);
        if (family && f.EligibleChildren is null) throw Missing("מספר ילדים עד גיל הזכאות");
        var person = new SampleEmployee("eligibility", "הפרופיל שלך", o.Name, o.Group, 0, 0, 0, 0, 0, 0, f.EligibleChildren ?? 0, "", 0, new Dictionary<string, string>());
        return data.Advisor.CheckEligibility(person, Choice(request.Alternative));
    }
    private string? Choice(string value) => value == "waiver" ? null : data.Catalog.ContainsKey(value) ? value : throw new CalculationException("מזהה הרכב אינו קיים בקטלוג המקור.");
    public BenefitCalculation CalculateNet(NetCalculation request) => data.Advisor.Calculate(Employee(request.Facts, data.Rules.Policy.Family.Cars.Contains(request.Choice)), Choice(request.Choice), request.Participation);
    public SalaryComparison CompareScenarios(ScenarioComparison request)
    {
        var employee = Employee(request.Facts, data.Rules.Policy.Family.Cars.Contains(request.Alternative) || request.Facts.CurrentCar is { } current && data.Rules.Policy.Family.Cars.Contains(current));
        if (request.Facts.CurrentCar is null) throw Missing("מסלול נוכחי");
        return new(data.Advisor.Calculate(employee, Choice(request.Facts.CurrentCar), employee.CurrentParticipation),
            data.Advisor.Calculate(employee, Choice(request.Alternative), request.Participation));
    }
    public RepresentativeTicket EscalateToRepresentative(RepresentativeEscalation request)
    {
        var f = request.Facts; var group = f.Occupation is { } name && data.Occupations.TryGetValue(name, out var o) ? o.Group : "טרם נבחר";
        var category = request.Category == "Participation" ? "Participation" : "BusinessQuestion";
        return new(AdvisorStore.Id(), request.ConversationId, "Open", category,
            category == "Participation" ? "נא לאשר את סכום השתתפות העובד הבסיסי לרכב שנבחר (ללא חלק הפער המשפחתי)." : "נדרשת הבהרה עסקית לגבי בחירת מסלול הרכב; אין סמכות לשנות כללי מס או זכאות.",
            request.VehicleId, group, f.Occupation ?? "טרם נבחר", AdvisorStore.Now(), AdvisorStore.Now(), 0, request.ForCurrent ? "current" : "alternative");
    }
}
