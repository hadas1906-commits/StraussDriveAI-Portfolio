using System.Text.RegularExpressions;

namespace StraussDrive.Api.Advisor;

public static class IntentHints
{
    public static string? CanonicalGroup(string? value) => value?.ToUpperInvariant() switch
    { "A" => "א", "B" => "ב", "C" => "ג", "D" => "ד", var group => group };
    public static bool ElectricComparison(string text) => text.Contains("חשמל", StringComparison.Ordinal)
        && Regex.IsMatch(text, @"השוו|להשוות|תשוו|לעומת|מול");
    public static bool GroupClaim(string text) => Group(text) is not null
        && Regex.IsMatch(text, @"(?:אני|אנחנו)\s+(?:(?:שייך|שייכת|שייכים)\s+)?(?:ב|מ|ל)?קבוצ|הקבוצה\s+שלי");
    public static bool ElectricOptions(string text) => text.Contains("חשמל", StringComparison.Ordinal)
        && Regex.IsMatch(text, @"אילו|איזה\s+(?:כלי\s+)?רכב|אפשרויות|מהם|רשימ|מה\s+האופציות");
    public static string? Group(string text)
    {
        var match = Regex.Match(text, @"(?:קבוצ(?:ה|ת)|group)\s*(?:שלי\s*)?(?:היא\s*)?[-:]?\s*([A-Za-zאבגד])(?![\p{L}])", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }
    public static bool Representative(string text, string? pendingField) =>
        !Regex.IsMatch(text, @"(?:לא|בלי|ללא)\s+(?:צריך\s+|צריכה\s+)?נציג") &&
        (Regex.IsMatch(text, @"(?:צריך|צריכה|נדרש|נדרשת|מבקש|מבקשת|רוצה|פנה|לפנות|העבר|העביר|שיחה\s+עם)\s+(?:אל\s+|ל)?נציג|(?:לדבר|לשוחח|להתייעץ)\s+עם\s+(?:ה)?נציג|נציג\s+(?:בבקשה|לאישור|אנושי)|^נציג[?.!\s]*$")
        || text.Contains("אישור השתתפות", StringComparison.Ordinal)
        || (Regex.IsMatch(text, @"לא\s+(?:ידוע|יודע|יודעת)") && (text.Contains("השתתפות") || pendingField == "currentParticipation")));
}
