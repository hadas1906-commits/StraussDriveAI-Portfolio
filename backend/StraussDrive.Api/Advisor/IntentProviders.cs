using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StraussDrive.Import;
namespace StraussDrive.Api.Advisor;

public interface IConversationProvider
{
    string Mode { get; }
    bool UsesNativeCapabilities => false;
    Task<TurnPlan> PlanAsync(string text, ConversationState state, CancellationToken cancellation);
}
public sealed class DemoConversationProvider(HackathonData data) : IConversationProvider
{
    public string Mode => "deterministic-demo";
    public Task<TurnPlan> PlanAsync(string text, ConversationState state, CancellationToken cancellation)
    {
        var t = text.Trim();
        if (IntentHints.ElectricOptions(t)) return Task.FromResult(new TurnPlan("electric_options", GroupQuery: IntentHints.Group(t)));
        decimal? Number(string pattern)
        {
            var match = Regex.Match(t, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            var group = match.Groups[1]; var raw = group.Value; var end = group.Index + group.Length;
            if (end < t.Length && ("eE,.".Contains(t[end]))) return null;
            if (raw.Contains(',') && !Regex.IsMatch(raw, @"^\d{1,3}(,\d{3})+(\.\d+)?$")) return null;
            return decimal.TryParse(raw.Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
        }
        var facts = new EmployeeFacts(
            Occupation: data.Occupations.Keys.FirstOrDefault(name => t.Contains(name, StringComparison.Ordinal)),
            BaseSalary: Number(@"(?:שכר(?:\s+בסיס)?|בסיס)\s*(?:ל|הוא|:|=)?\s*([\d,.]+)"),
            Commission: Number(@"עמלות\s*(?:ל|:|=)?\s*([\d,.]+)"), Bonus: Number(@"בונוס\s*(?:ל|:|=)?\s*([\d,.]+)"),
            PensionPercent: Number(@"פנסיה\s*(?:ל|:|=)?\s*([\d.]+)"), StudyFundPercent: Number(@"(?:קרן\s+השתלמות|השתלמות)\s*(?:ל|:|=)?\s*([\d.]+)"),
            CreditPoints: Number(@"נקודות\s+זיכוי\s*(?:ל|:|=)?\s*([\d.]+)"));
        var children = Number(@"([\d]+)\s*ילדים") ?? Number(@"ילדים\s*[:=]?\s*([\d]+)");
        if (children is >= 0 and <= 100 && children == decimal.Truncate(children.Value)) facts = facts with { EligibleChildren = (int)children.Value };
        if (state.PendingField is { } field)
        {
            var n = Number(@"^\s*([\d,.]+)\s*%?\s*$");
            if (n.HasValue) facts = field switch
            {
                "baseSalary" => facts with { BaseSalary = n },
                "commission" => facts with { Commission = n },
                "bonus" => facts with { Bonus = n },
                "pensionPercent" => facts with { PensionPercent = n },
                "studyFundPercent" => facts with { StudyFundPercent = n },
                "creditPoints" => facts with { CreditPoints = n },
                "currentParticipation" => facts with { CurrentParticipation = n },
                "eligibleChildren" when n is >= 0 and <= 100 && n == decimal.Truncate(n.Value) => facts with { EligibleChildren = (int)n.Value },
                _ => facts
            };
            if (field == "currentCar")
            {
                if (t.Contains("ויתור") || t.Contains("אין רכב")) facts = facts with { CurrentCar = "waiver" };
                else { var matches = FindCars(t); if (matches.Length == 1) facts = facts with { CurrentCar = matches[0] }; }
            }
        }
        var cars = FindCars(t);
        if (cars.Length > 1 && state.Facts.CurrentCar is { } current) cars = cars.Where(id => id != current).ToArray();
        if (IntentHints.ElectricComparison(t))
            cars = cars.Where(id => data.Catalog[id].Drive == data.Rules.Policy.DriveCodes["3"]).ToArray();
        var explicitId = Regex.Match(t, @"(?<![\p{L}\p{N}-])DEMO-\d+(?![\p{L}\p{N}-])", RegexOptions.IgnoreCase);
        var waiver = t.Contains("לוותר") || t.Contains("ויתור");
        if (facts == new EmployeeFacts() && cars.Length == 0 && !explicitId.Success
            && !IntentHints.Representative(t, state.PendingField) && state.Alternative is { } alternative
            && data.Rules.Policy.Family.Cars.Contains(alternative)
            && t.Contains("פער") && Regex.IsMatch(t, @"תקציב|עובד|מעסיק|חלק"))
            return Task.FromResult(new TurnPlan("tools") { Commands = [new("explain_calculation", JsonSerializer.SerializeToElement(new { }))] });
        var intent = t.Contains("פירוט") || t.Contains("פירוק") ? "breakdown" : t.Contains("זכאי") || t.Contains("זכאות") ? "eligibility" : waiver ? "waiver" : t.Contains("משפח") ? "family" : t.Contains("חשמל") ? "electric" : cars.Length > 0 || t.Contains("השווא") ? "compare" : state.Intent;
        var representative = IntentHints.Representative(t, state.PendingField);
        var recognized = cars.Length > 0 || explicitId.Success || waiver || representative || facts != new EmployeeFacts()
            || t.Contains("פירוט") || t.Contains("פירוק") || t.Contains("זכאי") || t.Contains("זכאות") || t.Contains("משפח")
            || t.Contains("חשמל") || t.Contains("השווא");
        var presentationFollowup = Regex.IsMatch(t, @"משתלם|משתלמת|הכי|ההפרש|מפסידה|מפסיד|טבלה|ביניהם|שניהם|מהם");
        if (facts == new EmployeeFacts() && !representative && state.PendingField is null
            && (cars.Length > 1 || state.RecentComparisons.Length > 1 && (presentationFollowup || cars.Length > 0)))
        {
            // Preserve mention order, rather than catalog enumeration order.
            var ordered = cars.Take(3).ToArray();
            return Task.FromResult(new TurnPlan("tools", t) { Commands = [new("calculate_vehicle_comparison",
                JsonSerializer.SerializeToElement(new CapabilityArguments(Vehicles: ordered.Length == 0 ? null : ordered,
                    Recommend: presentationFollowup && !t.Contains("רק טבלה"), TableOnly: t.Contains("רק טבלה"),
                    IncludeRecent: t.Contains("גם") || t.Contains("מפסיד")), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }))] });
        }
        if (!recognized) return Task.FromResult(new TurnPlan("clarify_domain"));
        return Task.FromResult(new TurnPlan(intent, waiver ? "waiver" : cars.Length == 1 ? cars[0] : cars.Length > 1 || explicitId.Success ? t : null, facts, representative,
            t.Contains("השתתפות") ? "Participation" : "BusinessQuestion"));
    }
    private string[] FindCars(string text)
    {
        var matches = VehicleSearch.Lookup(data.Catalog.Values, text);
        return matches.Length > 0 ? matches.Select(c => c.Id).ToArray() : VehicleSearch.Mentions(data.Catalog.Values, text);
    }
}
