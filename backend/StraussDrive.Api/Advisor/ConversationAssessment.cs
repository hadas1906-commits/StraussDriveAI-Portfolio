namespace StraussDrive.Api.Advisor;

public sealed record InputStatus(string Field, string Source, string Status);
public sealed record ConversationAssessment(string Status, string[] MissingFields, string[] Conflicts, InputStatus[] Inputs, string NextStep);
public static class ConversationReadiness
{
    public static ConversationAssessment Assess(ConversationState state)
    {
        var conflicts = state.Claims.TryGetValue("group", out var claimed) && claimed != state.VerifiedGroup
            ? new[] { state.VerifiedGroup is null ? "טענת קבוצה לא מאומתת. עיסוק הוא תנאי לבדיקת הזכאות בפרופיל אישי."
                : $"טענת קבוצה {claimed} אינה תואמת לקבוצה המאומתת {state.VerifiedGroup}. ההשוואות ממשיכות לפי המקור המאומת." } : [];
        var pending = state.PendingField ?? (state.Stage == "GatheringFacts" && state.Facts.Occupation is null ? "occupation" : null);
        var missing = pending is { } field ? new[] { field } : Array.Empty<string>();
        var status = state.ProposedFacts is not null ? "awaiting_confirmation" : state.PendingTicketId is not null ? "waiting_for_representative"
            : missing.Length > 0 ? "needs_input" : conflicts.Length > 0 ? "conflict" : "ready";
        var next = status switch {
            "awaiting_confirmation" => "בדקו את השינוי המוצע ואשרו או דחו אותו בכרטיס. עד האישור הוא לא קלט לחישוב.",
            "waiting_for_representative" => "הפנייה נשמרה. אפשר לעיין בכללים, להמתין לתשובה או לבקש סגירה מפורשת לפני שינוי חלופה.",
            "needs_input" => "השלימו רק את הנתון המסומן בשיחה או בטופס. אין צורך להזין שוב נתונים שכבר זמינים.",
            "conflict" => "אפשר להשוות לפי הזכאות המאומתת או לברר את המקור מול נציג. הטענה אינה משנה קבוצה או תקציב.",
            _ => "אפשר לבחור חלופה, לבקש הסבר או לבדוק תרחיש נפרד בלי לשנות את פרופיל המקור." };
        return new(status, missing, conflicts, state.Evidence.Select(e => new InputStatus(e.Key, e.Value.Source, e.Value.Status)).ToArray(), next);
    }
}
