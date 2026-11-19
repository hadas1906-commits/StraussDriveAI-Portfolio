using System.Security.Cryptography;
using System.Text;
using StraussDrive.Domain;
using StraussDrive.Import;

namespace StraussDrive.Api;

public sealed record DecisionSource(string Kind, string Name, string Sha256);
public sealed record DecisionVehicle(string Scenario, string CarId, VehicleKey Key, decimal ValueOfUse, string Url);
public sealed record DecisionEvidence(string Outcome, string InputKind, string ParticipationSource, string RuleVersion,
    int PayrollTaxYear, string SourceFingerprint, string[] Rules, string[] Assumptions, DecisionSource[] Sources, DecisionVehicle[] Vehicles);

public static class DecisionEvidenceBuilder
{
    public static string Fingerprint(HackathonData data) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join("|", data.Sources.OrderBy(s => s.FileName, StringComparer.Ordinal).Select(s => s.Sha256))))).ToLowerInvariant();
    public static DecisionEvidence Build(HackathonData data, SalaryComparison comparison, string inputKind, string participationSource)
    {
        var policy = data.Rules.Policy;
        var rules = new List<string> {
            "הזכאות נגזרת מהעיסוק וקבוצת המקור; טענה בשיחה אינה משנה זכאות.",
            "רכב רגיל חייב להיות בקבוצה המותרת או נמוכה ממנה ובמסגרת התקציב. יתרת תקציב אינה משולמת בשכר.",
            $"החישוב החודשי משתמש בלוחות {data.Rules.PayrollTaxYear}. שווי שימוש מגדיל הכנסה חייבת; השתתפות יורדת מהנטו בלבד.",
            "מנוע החישוב שומר דיוק עשרוני; עיגול לשקל מתבצע בתצוגה בלבד." };
        if (comparison.Alternative.Scenario.Car is null)
            rules.Add("ויתור נבדק לפי העיסוק. אחזקת הרכב חייבת במס ואינה בסיס להפרשות סוציאליות.");
        if (comparison.Alternative.Scenario.IncludeElectricReimbursement)
            rules.Add($"החזר החשמל מגולם במנוע לתוספת של {data.Rules.Business.ElectricReimbursementNetTarget} ₪ נטו לפי הכלל הפיקטיבי.");
        if (comparison.Alternative.Scenario.Car is { } selected && policy.Family.Cars.Contains(selected.Id))
            rules.Add($"המסלול המשפחתי דורש {policy.Family.MinChildren} ילדים עד גיל {policy.Family.ChildMaxAge}. הפער החיובי מעל התקציב מתחלק לפי קובץ הכללים.");
        var assumptions = new List<string> {
            "נתוני הפורטפוליו פיקטיביים. זו הערכת שכר לפי הקלט והכללים, לא התחייבות לתלוש שכר.",
            "כל הטבלאות לשנת הדגמה 2099; אין שימוש בדין מס או בנתוני רכב אמיתיים.",
            "ההשלכה השנתית מניחה 12 חודשים ללא שינוי בתנאים." };
        assumptions.Add(participationSource == "imported_current" ? "המסלול הנוכחי לא הוחלף; סכום ההשתתפות מהמקור נשמר גם בתרחיש."
            : participationSource == "representative_approval" ? "השתתפות החלופה מבוססת על תשובת הנציג בשיחה זו."
            : "השתתפות החלופה היא ברירת המחדל בקטלוג, ואינה אישור פרטני מנציג.");
        if (inputKind != "imported_sample") assumptions.Add("רכיבי השכר כוללים קלט לתרחיש או לשיחה; פרופיל השכר המיובא לא עודכן.");
        var vehicles = new List<DecisionVehicle>();
        foreach (var (label, calculation) in new[] { ("current", comparison.Current), ("alternative", comparison.Alternative) })
            if (calculation.Scenario.Car is { } car && data.Catalog.TryGetValue(car.Id, out var catalog))
                vehicles.Add(new(label, car.Id, catalog.Key, data.VehicleValues[catalog.Key].ValueOfUse, $"/api/advisor/sources/vehicles/{car.Id}"));
        return new("calculated_estimate", inputKind, participationSource, data.Rules.Version, data.Rules.PayrollTaxYear, Fingerprint(data),
            rules.ToArray(), assumptions.ToArray(), data.Sources.Select(s => new DecisionSource(Path.GetExtension(s.FileName).TrimStart('.'), s.FileName, s.Sha256)).ToArray(), vehicles.ToArray());
    }
}
