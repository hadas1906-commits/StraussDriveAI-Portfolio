using System.Security.Claims;
using System.Text.Json;
using StraussDrive.Api.Advisor;
using StraussDrive.Import;

namespace StraussDrive.Api;

public sealed record RuntimeQuality(int AssistantTurns, int ProviderAttempts, int ProviderResponses, int FallbackTurns, int DemoTurns,
    int ToolCalls, int CompletedToolCalls, int FailedToolCalls, int NeedsInputToolCalls, int ComparisonsProduced,
    double? MedianProviderLatencyMs, bool GroundednessEvaluated = false);
public sealed record ScriptedQuality(int Scenarios, int Completed, int Grounded, int UnnecessaryQuestions, int UnsafeOutcomes,
    int HandoffJourneys, int PersistedHandoffs, double? MedianToolLatencyMs);
public sealed record EvaluationSnapshot(int SchemaVersion, string Status, string? RunId, string? RecordedAt,
    string? SourceFingerprint, ScriptedQuality? Scripted, object? Live = null, string? CodeCommit = null);
public sealed record QualityView(string Scope, RuntimeQuality Runtime, EvaluationSnapshot Evaluation, string[] Limitations);

public static class QualityDashboard
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public static RuntimeQuality Measure(IEnumerable<ConversationMessage> messages)
    {
        var turns = messages.Where(m => m.Role == "assistant").ToArray();
        var tools = turns.SelectMany(t => t.Tools).ToArray();
        var latency = turns.Where(t => t.Provider is { LatencyMs: > 0 }).Select(t => t.Provider!.LatencyMs).Order().ToArray();
        return new(turns.Length, turns.Count(t => t.Provider is not null), turns.Count(t => t.Provider?.Outcome == "live_llm"),
            turns.Count(t => t.Mode == "deterministic-fallback"), turns.Count(t => t.Mode == "deterministic-demo"),
            tools.Length, tools.Count(t => t.Status == "completed"), tools.Count(t => t.Status == "failed"), tools.Count(t => t.Status == "needs_input"),
            turns.Sum(t => t.Comparisons?.Length ?? (t.Comparison is null ? 0 : 1)), latency.Length == 0 ? null : (latency[(latency.Length - 1) / 2] / 2d + latency[latency.Length / 2] / 2d));
    }
    public static EvaluationSnapshot Snapshot(HackathonData data)
    {
        using var stream = typeof(QualityDashboard).Assembly.GetManifestResourceStream("StraussDrive.Api.Evaluation.quality-evidence.json");
        var snapshot = stream is null ? null : JsonSerializer.Deserialize<EvaluationSnapshot>(stream, Json);
        return Bind(snapshot, data);
    }
    public static EvaluationSnapshot Bind(EvaluationSnapshot? snapshot, HackathonData data)
    {
        if (snapshot is null || snapshot.SchemaVersion != 1 || snapshot.Status != "measured" || snapshot.Scripted is null
            || string.IsNullOrWhiteSpace(snapshot.RunId) || !DateTimeOffset.TryParse(snapshot.RecordedAt, out _))
            return new(1, "unavailable", null, null, null, null);
        var metrics = snapshot.Scripted;
        if (metrics.Scenarios <= 0 || metrics.Completed < 0 || metrics.Completed > metrics.Scenarios || metrics.Grounded < 0 || metrics.Grounded > metrics.Completed
            || metrics.UnnecessaryQuestions < 0 || metrics.UnnecessaryQuestions > metrics.Scenarios || metrics.UnsafeOutcomes < 0 || metrics.UnsafeOutcomes > metrics.Scenarios
            || metrics.HandoffJourneys <= 0 || metrics.PersistedHandoffs < 0 || metrics.PersistedHandoffs > metrics.HandoffJourneys)
            return new(1, "unavailable", null, null, null, null);
        // Evidence is bound to the actual fictional inputs. Changed source data invalidates the displayed evaluation.
        if (snapshot.SourceFingerprint != DecisionEvidenceBuilder.Fingerprint(data))
            return snapshot with { Status = "stale_source", Scripted = null, Live = null };
        return snapshot with { Live = null }; // No live-language evaluation has been imported or asserted by this release.
    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/advisor/quality", (HttpContext ctx, AdvisorStore store, HackathonData data) => Results.Ok(new QualityView(
            "current_account_recent_400_messages", Measure(store.OwnedQualityMessages(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!)), Snapshot(data),
            ["מדדי השימוש מתייחסים לחשבון הנוכחי בלבד ואינם מציגים שכר, זהויות או תוכן שיחות.",
             "תוצאת ספק שנצפתה אינה מדידת הבנת עברית. בחירה נכונה של כלים ו-groundedness נבדקות בנפרד.",
             "בדיקות התסריטים משתמשות בבחירת כלים מתוסרטת; הן אינן בדיקות מודל חי. זמן כלי מקומי אינו זמן תגובת מודל.",
             "אין תוצאת groundedness עצמאית להיסטוריית השימוש, ואין מדידת איכות מודל חי ללא הערכה אמיתית."])))
            .RequireAuthorization("Employee").RequireRateLimiting("AdvisorTurns");
    }
}
