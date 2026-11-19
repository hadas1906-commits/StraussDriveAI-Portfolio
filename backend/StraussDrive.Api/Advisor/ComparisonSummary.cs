using System.Globalization;

namespace StraussDrive.Api.Advisor;

public sealed record ComparisonSummaryRow(string VehicleId, string Name, bool Baseline, string Status,
    string Explanation, decimal? MonthlyNet = null, decimal? MonthlyDifference = null, decimal? AnnualDifference = null);
public sealed record ComparisonSummary(ComparisonSummaryRow[] Rows, string? Recommendation, bool TableOnly);

/// <summary>Presentation only: money and scenario identity come from validated engine results.</summary>
public static class ComparisonSummaries
{
    public static bool SameScenario(ScenarioDto a, ScenarioDto b) => a.CarId == b.CarId
        && a.GrossSalary == b.GrossSalary && a.ValueOfUse == b.ValueOfUse
        && a.MaintenanceAllowance == b.MaintenanceAllowance && a.FamilyEmployeeShare == b.FamilyEmployeeShare
        && a.Salary == b.Salary;

    public static ComparisonSummary Build(ComparisonSummaryRow[] attempts, ComparisonDto[] comparisons, bool recommend, bool tableOnly, ScenarioDto? baseline = null)
    {
        baseline ??= comparisons.FirstOrDefault()?.Current;
        var unchanged = comparisons.Where(c => SameScenario(c.Current, c.Alternative)).ToArray();
        var unique = comparisons.Where(c => !SameScenario(c.Current, c.Alternative)).DistinctBy(c => c.Alternative.CarId ?? "waiver").ToArray();
        attempts = attempts.Where(a => !unchanged.Any(c => (c.Alternative.CarId ?? "waiver") == a.VehicleId)).DistinctBy(a => a.VehicleId).ToArray();
        var rows = new List<ComparisonSummaryRow>();
        if (baseline is not null)
            rows.Add(new(baseline.CarId ?? "waiver", baseline.Name, true, "current", "המסלול הנוכחי לפי הנתונים המאומתים",
                baseline.Salary.DisplayNetSalary, 0m, 0m));
        else rows.Add(new("current", "המסלול הנוכחי", true, "unavailable", "אין חישוב תקף להצגת הנטו הנוכחי"));
        foreach (var attempt in attempts)
        {
            var result = unique.FirstOrDefault(c => (c.Alternative.CarId ?? "waiver") == attempt.VehicleId);
            var name = attempts.Count(r => r.Name == attempt.Name) > 1 ? $"{attempt.Name} ({attempt.VehicleId})" : attempt.Name;
            if (result is not null && result.Alternative.CarId == baseline?.CarId) name += " — שינוי בהשתתפות";
            var explanation = result?.Alternative.Benefit?.EligibilityReason ?? attempt.Explanation;
            if (result?.Alternative.FamilyEmployeeShare > 0)
                explanation += $" חלק העובד בפער התקציב: {Money(result.Alternative.FamilyEmployeeShare)}; חלק המעסיק: {Money(result.Alternative.Benefit!.FamilyEmployerShare)}. חלק העובד כבר כלול בהשתתפות הכוללת.";
            if (result is not null && result.Alternative.CarId is null)
                explanation += $" אחזקת רכב חייבת במס: {Money(result.Alternative.MaintenanceAllowance)}; אינה מגדילה את השכר הפנסיוני.";
            rows.Add(result is null ? attempt : attempt with {
                Name = name, Status = "completed", Explanation = explanation,
                MonthlyNet = result.Alternative.Salary.DisplayNetSalary, MonthlyDifference = result.DisplayNetDifference,
                AnnualDifference = result.DisplayAnnualDifference });
        }
        string? recommendation = null;
        if (recommend && !tableOnly)
        {
            var failed = attempts.Where(a => !unique.Any(c => (c.Alternative.CarId ?? "waiver") == a.VehicleId)).ToArray();
            var reasons = string.Join(" ", failed.Select(a => a.Explanation).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            if (unique.Length == 0)
            {
                recommendation = failed.Any(a => a.Status == "ineligible") ? reasons
                    : baseline is not null && attempts.Length == 0 ? "זהו המסלול הנוכחי עצמו; אין שינוי בנטו ואין חלופה חדשה לבחירה."
                    : reasons + " אין חלופה עם חישוב תקף וזכאות מאומתת; לא ניתן להמליץ לפי נטו.";
            }
            else
            {
                var bestNet = unique.Max(c => c.Alternative.Salary.NetSalary);
                var best = unique.Where(c => c.Alternative.Salary.NetSalary == bestNet).ToArray();
                var names = string.Join(" ו־", best.Select(c => c.Alternative.Name));
                if (baseline is not null && baseline.Salary.NetSalary > bestNet)
                {
                    recommendation = "הישארות במסלול הנוכחי עדיפה מבחינת הנטו בלבד.";
                    foreach (var other in unique)
                        recommendation += other.DisplayNetDifference == 0
                            ? $" הנטו המוצג עם {other.Alternative.Name} זהה לשקל; לפני עיגול הוא נמוך ב־{Money(baseline.Salary.NetSalary - other.Alternative.Salary.NetSalary)} בחודש."
                            : $" מעבר ל־{other.Alternative.Name} יפחית את הנטו המוצג ב־{Money(Math.Abs(other.DisplayNetDifference))} בחודש לעומת {baseline.Name} הנוכחי (הפער לפני עיגול: {Money(baseline.Salary.NetSalary - other.Alternative.Salary.NetSalary)}).";
                    recommendation += " ייתכנו שיקולים משפחתיים ואישיים אחרים שאינם משתקפים בחישוב.";
                }
                else if (baseline is not null && baseline.Salary.NetSalary == bestNet)
                    recommendation = $"מבחינת הנטו החודשי יש שוויון בין הישארות במסלול הנוכחי לבין {names}; אין יתרון נטו במעבר לחלופות האלו.";
                else
                {
                    recommendation = best.Length > 1 ? $"בהתבסס על הנטו החודשי בלבד, יש שוויון בין {names}."
                        : $"בהתבסס על הנטו החודשי בלבד, {names} היא החלופה המשתלמת ביותר מבין החלופות שחושבו בהצלחה ונמצאו בזכאות.";
                    if (baseline is not null)
                        recommendation += $" היתרון לעומת המסלול הנוכחי: {Money(bestNet - baseline.Salary.NetSalary)} בחודש (לפני עיגול התצוגה).";
                }
                foreach (var other in unique.Where(c => c.Alternative.Salary.NetSalary < bestNet))
                    recommendation += $" היתרון של {names} לעומת {other.Alternative.Name}: {Money(bestNet - other.Alternative.Salary.NetSalary)} בחודש (לפני עיגול התצוגה).";
                recommendation += " זו השוואה כספית בלבד, ולא קביעה לגבי איכות, נוחות, טווח או בטיחות הרכב.";
                if (failed.Length > 0) recommendation = reasons + " " + recommendation + " חלופות ללא חישוב תקף אינן נכללות בדירוג.";
            }
        }
        return new(rows.ToArray(), recommendation, tableOnly);
    }
    private static string Money(decimal value) => value > 0 && value < 0.01m ? "פחות מ־0.01 ₪" : value.ToString("N2", CultureInfo.GetCultureInfo("he-IL")) + " ₪";
}
