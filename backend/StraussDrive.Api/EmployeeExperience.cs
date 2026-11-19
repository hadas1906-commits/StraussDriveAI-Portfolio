using System.Text.Json.Serialization;
using StraussDrive.Domain;
using StraussDrive.Import;

namespace StraussDrive.Api;

public sealed record EligibilityDto(bool Eligible, string Reason, decimal Budget, decimal FamilyEmployeeShare, decimal FamilyEmployerShare);
public sealed record EmployeeDto(string Id, string Name, string Occupation, string Group, decimal GroupBudget,
    decimal BaseSalary, decimal Commission, decimal Bonus, decimal PensionRate, decimal StudyFundRate,
    decimal CreditPoints, int EligibleChildren, string? CurrentCarId, string CurrentCar,
    EligibilityDto Waiver, decimal MaintenanceAllowance, SalaryBreakdown CurrentSalary);
public sealed record VehicleDto(string Id, string Name, string Manufacturer, string Drive, string Group,
    decimal MonthlyCost, decimal Participation, decimal ValueOfUse, VehicleKey Key, EligibilityDto Eligibility);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SalaryInputs(decimal BaseSalary, decimal Commission, decimal Bonus, decimal PensionPercent,
    decimal StudyFundPercent, decimal CreditPoints);
public sealed record SimulationRequest(string EmployeeId, string? AlternativeCarId, SalaryInputs Inputs);
public sealed record ExperienceComparisonRequest(string EmployeeId, string? AlternativeCarId);
public sealed record ScenarioDto(string? CarId, string Name, decimal GrossSalary, decimal ValueOfUse,
    decimal MaintenanceAllowance, decimal FamilyEmployeeShare, SalaryBreakdown Salary, BenefitContextDto? Benefit = null);
public sealed record BenefitContextDto(decimal Budget, decimal MonthlyCost, decimal FamilyEmployerShare, string EligibilityReason);
public sealed record WaterfallPoint(string Name, decimal Base, decimal Amount, decimal Change, bool Total);
public sealed record ComparisonDto(string EmployeeId, ScenarioDto Current, ScenarioDto Alternative,
    decimal NetDifference, decimal DisplayNetDifference, decimal AnnualNetDifference, decimal DisplayAnnualDifference,
    IReadOnlyList<WaterfallPoint> Waterfall, DecisionEvidence? Evidence = null);

/// <summary>Presentation and validated simulation around the existing domain engine.</summary>
public static class EmployeeExperience
{
    public static void MapEmployeeExperience(this WebApplication app)
    {
        app.MapGet("/api/context", (HackathonData data) => Results.Ok(new
        {
            payrollTaxYear = data.Rules.PayrollTaxYear,
            ruleVersion = data.Rules.Version,
            vehicleCount = data.Catalog.Count,
            employeeCount = data.Employees.Count,
            groups = data.Rules.Policy.GroupOrder,
            electricNetBonus = data.Rules.Business.ElectricReimbursementNetTarget,
            familyMinChildren = data.Rules.Policy.Family.MinChildren,
            childMaxAge = data.Rules.Policy.Family.ChildMaxAge,
            sourceNote = data.RawRules.GetProperty("note").GetString()
        }));
        app.MapGet("/api/employees", (HackathonData data) => Results.Ok(data.Employees.Values.OrderBy(e => e.Id).Select(employee =>
        {
            var current = data.Advisor.Calculate(employee, CurrentId(data, employee), employee.CurrentParticipation);
            var occupation = data.Occupations[employee.Occupation];
            return new EmployeeDto(employee.Id, employee.Name, employee.Occupation, employee.Group,
                data.Rules.Policy.Budgets[employee.Group], employee.BaseSalary, employee.Commission, employee.Bonus,
                employee.PensionRate, employee.StudyFundRate, employee.CreditPoints, employee.EligibleChildren,
                CurrentId(data, employee), employee.CurrentCar, Eligibility(data, employee, null), occupation.MaintenanceAllowance, current.Salary);
        }).ToArray()));
        app.MapGet("/api/vehicles", (string employeeId, HackathonData data) =>
        {
            if (!data.Employees.TryGetValue(employeeId, out var employee)) return UnknownEmployee();
            return Results.Ok(data.Catalog.Values.OrderBy(c => c.Id).Select(car => new VehicleDto(
                car.Id, car.CommercialName, car.Manufacturer, car.Drive, car.Group, car.MonthlyCost,
                car.DefaultParticipation, data.VehicleValues[car.Key].ValueOfUse, car.Key, Eligibility(data, employee, car.Id))).ToArray());
        });
        app.MapPost("/api/experience/compare", (ExperienceComparisonRequest request, HackathonData data) => Calculate(data, request.EmployeeId, request.AlternativeCarId, null));
        app.MapPost("/api/experience/simulate", (SimulationRequest request, HackathonData data) =>
        {
            if (request.Inputs is null) return Results.Problem(statusCode: 422, title: "נתונים חסרים", detail: "יש להזין את כל רכיבי השכר וההפרשות.");
            return Calculate(data, request.EmployeeId, request.AlternativeCarId, request.Inputs);
        });
    }
    private static IResult UnknownEmployee() => Results.Problem(statusCode: 422, title: "עובד לא נמצא", detail: "יש לבחור עובד מתוך נתוני הפורטפוליו.");
    private static string? CurrentId(HackathonData data, SampleEmployee employee) => data.ResolvePortfolioSelection(data.References[employee.Id].Current);
    private static EligibilityDto Eligibility(HackathonData data, SampleEmployee employee, string? carId)
    {
        var check = data.Advisor.CheckEligibility(employee, carId);
        var family = carId is not null && data.Rules.Policy.Family.Cars.Contains(carId);
        var reason = carId is null
            ? check.Eligible ? "העיסוק מאפשר ויתור על הרכב וקבלת אחזקת רכב חייבת במס." : "העיסוק מחייב רכב חברה ואינו מאפשר ויתור."
            : family
                ? check.Eligible ? "זכאות למסלול המשפחתי; ההשתתפות כוללת את חלק העובד בפער התקציב."
                    : $"נדרשים לפחות {data.Rules.Policy.Family.MinChildren} ילדים עד גיל {data.Rules.Policy.Family.ChildMaxAge} למסלול המשפחתי."
                : check.Eligible ? "הרכב בקבוצת הזכאות שלך או בקבוצה נמוכה יותר, ובמסגרת התקציב."
                    : $"הרכב מחוץ לקבוצת הזכאות או מעבר לתקציב קבוצה {employee.Group}.";
        return new(check.Eligible, reason, check.Budget, check.FamilyEmployeeShare, check.FamilyEmployerShare);
    }
    private static IResult Calculate(HackathonData data, string employeeId, string? carId, SalaryInputs? inputs)
    {
        if (string.IsNullOrWhiteSpace(employeeId) || !data.Employees.TryGetValue(employeeId, out var original)) return UnknownEmployee();
        if (inputs is not null && (inputs.BaseSalary is < 0 or > 100000000 || inputs.Commission is < 0 or > 100000000 || inputs.Bonus is < 0 or > 100000000 || inputs.CreditPoints is < 0 or > 100 ||
            inputs.PensionPercent is < 0 or > 100 || inputs.StudyFundPercent is < 0 or > 100))
            return Results.Problem(statusCode: 422, title: "נתוני שכר לא תקינים", detail: "סכומי השכר והזיכויים חייבים להיות לא שליליים; אחוזי ההפרשה חייבים להיות בין 0 ל־100.");
        var employee = inputs is null ? original : original with
        {
            BaseSalary = inputs.BaseSalary,
            Commission = inputs.Commission,
            Bonus = inputs.Bonus,
            PensionRate = inputs.PensionPercent / 100m,
            StudyFundRate = inputs.StudyFundPercent / 100m,
            CreditPoints = inputs.CreditPoints
        };
        try
        {
            if (carId is not null && !data.Catalog.ContainsKey(carId))
                return Results.Problem(statusCode: 422, title: "רכב לא נמצא", detail: "יש לבחור מזהה רכב מהקטלוג הפיקטיבי.");
            var eligibility = Eligibility(data, employee, carId);
            if (!eligibility.Eligible) return Results.Problem(statusCode: 422, title: "החלופה אינה בזכאות", detail: eligibility.Reason);
            var c = data.Advisor.Compare(employee, CurrentId(data, employee), carId);
            return Results.Ok(Present(employee.Id, c, data, inputs is null ? "imported_sample" : "hypothetical"));
        }
        catch (CalculationException ex) { return Results.Problem(statusCode: 422, title: "לא ניתן לחשב", detail: ex.Message); }
        catch (OverflowException) { return Results.Problem(statusCode: 422, title: "סכום מחוץ לטווח", detail: "סכומי השכר חורגים מטווח החישוב הנתמך."); }
    }
    public static ComparisonDto Present(string employeeId, SalaryComparison c, HackathonData? data = null, string inputKind = "conversation_inputs", string participationSource = "catalog_default")
    {
        ScenarioDto Scenario(BenefitCalculation b) => new(b.Scenario.Car?.Id, b.Scenario.Car?.Name ?? "ויתור על רכב • אחזקת רכב",
            b.Scenario.Employee.MonthlyGrossSalary, b.Scenario.Car?.MonthlyTaxableValueOfUse ?? 0,
            b.Scenario.TaxableMaintenanceAllowance, b.Eligibility.FamilyEmployeeShare, b.Salary,
            new(b.Eligibility.Budget, b.Eligibility.MonthlyCost, b.Eligibility.FamilyEmployerShare,
                b.Scenario.Car is null ? "העיסוק מאפשר ויתור וקבלת אחזקת רכב חייבת במס, ללא הפרשות סוציאליות על האחזקה."
                    : b.Eligibility.FamilyEmployeeShare > 0 || b.Eligibility.FamilyEmployerShare > 0
                        ? "החלופה עומדת בתנאי המסלול המשפחתי. הפער מעל התקציב מתחלק לפי הכללים הפיקטיביים."
                        : "הרכב במסגרת הזכאות והתקציב לפי הכללים הפיקטיביים. תקציב שלא נוצל אינו משולם בשכר."));
        return new ComparisonDto(employeeId, Scenario(c.Current), Scenario(c.Alternative), c.NetDifference,
            c.DisplayNetDifference, c.NetDifference * 12, c.DisplayNetDifference * 12, Waterfall(c), data is null ? null : DecisionEvidenceBuilder.Build(data, c, inputKind, participationSource));
    }
    private static IReadOnlyList<WaterfallPoint> Waterfall(SalaryComparison c)
    {
        var a = c.Current.Salary; var b = c.Alternative.Salary; var cursor = a.NetSalary;
        var points = new List<WaterfallPoint> { new("נטו היום", 0, cursor, cursor, true) };
        void Add(string name, decimal change)
        {
            var end = cursor + change; points.Add(new(name, Math.Min(cursor, end), Math.Abs(change), change, false)); cursor = end;
        }
        Add("שכר ותוספות", b.GrossCashSalary - a.GrossCashSalary);
        Add("מס הכנסה", a.IncomeTax - b.IncomeTax);
        Add("ביטוח ובריאות", a.NationalInsurance + a.HealthInsurance - b.NationalInsurance - b.HealthInsurance);
        Add("הפרשות", a.EmployeePension + a.StudyFund - b.EmployeePension - b.StudyFund);
        Add("השתתפות", a.EmployeeNetParticipation - b.EmployeeNetParticipation);
        points.Add(new("נטו בחלופה", 0, b.NetSalary, b.NetSalary, true)); return points;
    }
}
