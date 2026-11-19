using StraussDrive.Domain;
using StraussDrive.Import;
namespace StraussDrive.Api.Advisor;

public sealed class ConversationAgent(HackathonData data, IAdvisorTools tools, IConversationProvider provider, DemoConversationProvider demo, ILogger<ConversationAgent>? logger = null)
{
    public string Mode => provider.Mode;
    public ConversationState Initial(string profile)
    {
        if (profile == "custom") return TrackInitial(new() { Stage = "GatheringFacts" }, "none");
        var e = data.Employees[profile]; var current = data.ResolvePortfolioSelection(data.References[e.Id].Current) ?? "waiver";
        return TrackInitial(new() { VerifiedGroup = e.Group, Facts = new(e.Occupation, e.BaseSalary, e.Commission, e.Bonus, e.PensionRate * 100, e.StudyFundRate * 100, e.CreditPoints, e.EligibleChildren, current, e.CurrentParticipation) }, "verified_import");
    }
    private static ConversationState TrackInitial(ConversationState state, string source)
    {
        foreach (var field in System.Text.Json.JsonSerializer.SerializeToElement(state.Facts).EnumerateObject())
            state.Evidence[field.Name] = field.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? new("none", "missing") : new(source, "usable");
        if (state.VerifiedGroup is not null) state.Evidence["VerifiedGroup"] = new("verified_import", "usable");
        return state;
    }
    public string Title(ConversationState state) => state.Alternative == "waiver" ? "בדיקת ויתור על רכב" : state.Alternative is { } id && data.Catalog.TryGetValue(id, out var c) ? "השוואה — " + c.CommercialName : "בירור זכאות ושכר";
    public static ConversationMessage Message(string role, string text, string mode = "deterministic-demo", ComparisonDto? comparison = null,
        SalaryBreakdown? breakdown = null, IEnumerable<ToolInvocation>? traces = null, IEnumerable<SourceLink>? sources = null) =>
        new(AdvisorStore.Id(), role, text, mode, AdvisorStore.Now(), comparison, breakdown, traces?.ToArray() ?? [], sources?.ToArray() ?? []);
    public ConversationMessage Welcome(string profile) => Message("assistant", profile == "custom"
        ? "נבנה את הפרופיל שלך בלי לנחש נתונים. אפשר להזין נתונים בטופס או בשיחה. איזה מסלול רכב ברצונך לבדוק?"
        : "נתוני עובד ההדגמה נטענו מהקובץ הפיקטיבי. אפשר להשוות רכב, לבדוק חשמלי או משפחתי, לוותר כשמותר או לבקש פירוט. איזה רכב או מסלול מעניין אותך?", "application");
    private static bool Confirmation(string text) => text.Trim().TrimEnd('.', '!') is "מאשר" or "מאשרת" or "מאשר/ת את הנתונים" or "מאשר את הנתונים" or "מאשרת את הנתונים";
    public async Task<AgentOutcome> Turn(Conversation c, MessageRequest request, CancellationToken cancellation)
    {
        ProviderDiagnostic? diagnostic = null;
        var before = c.State.Facts;
        var outcome = await TurnCore(c, request, cancellation, value => diagnostic = value);
        UpdateEvidence(c.State, request, before);
        return outcome with { Message = outcome.Message with { Provider = diagnostic } };
    }
    private async Task<AgentOutcome> TurnCore(Conversation c, MessageRequest request, CancellationToken cancellation, Action<ProviderDiagnostic> diagnose)
    {
        cancellation.ThrowIfCancellationRequested();
        RestoreVerifiedContext(c);
        if (request.FactDecision is { } decision)
        {
            if (decision is not ("confirm" or "discard") || c.State.ProposedFacts is null || request.Facts is not null
                || request.ConfirmHandoff is not null || request.RequestCancellation || request.ShareWithRepresentative)
                throw new AdvisorException(409, "השינוי המוצע השתנה. רעננו את השיחה ובדקו את הנתונים לפני אישור.");
            // Application actions carry intent explicitly; they do not depend on interpreting an approval phrase.
            request = request with { Text = decision == "confirm" ? "מאשר/ת את הנתונים" : "לא מאשר את הנתונים" };
        }

        if (request.RequestCancellation)
        {
            if (request.Facts is not null || request.ConfirmHandoff is not null || request.ShareWithRepresentative)
                throw new AdvisorException(422, "יש לשלוח בקשת סגירה בנפרד משינוי נתונים או מאישור אחר.");
            return new CapabilityOrchestrator(data, tools, this).Execute(c,
                [new("cancel_representative_request", System.Text.Json.JsonSerializer.SerializeToElement(new { }))], "application");
        }
        if (request.ConfirmHandoff is { } confirmation)
        {
            var proposal = c.State.ProposedHandoff;
            if (proposal is { Kind: "cancel" } && proposal.Id == confirmation && c.State.PendingTicketId is not null && proposal.TicketId == c.State.PendingTicketId)
            {
                var closed = c.State.PendingTicketId;
                c.State.PendingTicketId = null; c.State.ProposedHandoff = null; c.State.ConfirmedParticipation = null; c.State.ConfirmedVehicle = null;
                c.State.Stage = "SelectingAlternative";
                return new(Message("assistant", "הפנייה נסגרה לבקשתך, ללא אישור כספי. אפשר לבחור כעת חלופה אחרת.", "employee-confirmed"), ClosedTicket: closed);
            }
            if (proposal is null || proposal.Kind != "create" || proposal.Id != confirmation || c.State.PendingTicketId is not null
                || proposal.VehicleId != (proposal.Target == "current" ? c.State.Facts.CurrentCar : c.State.Alternative))
                throw new AdvisorException(409, "הבקשה השתנתה. יש לעיין במצב השיחה לפני אישור מחדש.");
            var ticket = tools.EscalateToRepresentative(new(c.Id, proposal.Category, proposal.VehicleId, c.State.Facts, proposal.Target == "current"));
            c.State.PendingTicketId = ticket.Id; c.State.ProposedHandoff = null; c.State.Stage = "WaitingForRepresentative";
            // The endpoint returns this message only after Save atomically commits ticket + messages + version.
            return new(Message("assistant", "הפנייה נשמרה והועברה לנציג. אפשר להמשיך לבדוק כללים; תשובת הנציג תופיע בשיחה הזו.",
                "employee-confirmed", traces: [new("create_representative_request", [ticket.Id])]), ticket);
        }
        if (!provider.UsesNativeCapabilities && LegacyGroupClaim(c, request.Text) is { } legacyClaim)
            return legacyClaim;
        if (c.State.VerifiedGroup is not null && request.Facts?.Occupation is { } requestedOccupation
            && requestedOccupation != data.Employees[c.ProfileKey].Occupation)
            return GroupConflict(c.State.VerifiedGroup);
        if (c.State.VerifiedGroup is not null && request.Facts?.EligibleChildren is { } requestedChildren
            && requestedChildren != data.Employees[c.ProfileKey].EligibleChildren)
            return new(Message("assistant", "מספר הילדים שבזכאות נקבע לפי הפרופיל המיובא ולא שונה לפי טענה. אפשר לברר נתון שגוי עם נציג; לא נעניק זכאות משפחתית ללא מקור מאומת.", "application"));
        if (c.State.ProposedFacts is not null && request.Facts is null
            && request.Text.Trim().TrimEnd('.', '!') is "לא מאשר את הנתונים" or "לא מאשרת את הנתונים" or "ביטול הנתונים")
        {
            c.State.ProposedFacts = null; c.State.ProposedPlan = null;
            c.State.Stage = c.State.PendingTicketId is null ? "GatheringFacts" : "WaitingForRepresentative";
            return new(Message("assistant", "הנתונים שהוצעו בוטלו ולא שימשו לחישוב. אפשר להזין תיקון בטופס או בהודעה חדשה.", "confirmed-input"));
        }
        var mode = Mode; TurnPlan plan;
        try
        {
            if (c.State.ProposedFacts is not null && Confirmation(request.Text))
            {
                plan = c.State.ProposedPlan ?? new(c.State.Intent); mode = "confirmed-input";
            }
            else if (request.Facts is not null) { plan = await demo.PlanAsync(request.Text, c.State, cancellation); mode = "structured-input"; }
            else if (provider is IDiagnosedConversationProvider diagnosed)
            {
                var result = await diagnosed.PlanWithDiagnosticsAsync(request.Text, c.State, cancellation);
                plan = result.Plan; diagnose(result.Diagnostic);
            }
            else plan = await provider.PlanAsync(request.Text, c.State, cancellation);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException or OperationCanceledException or KeyNotFoundException)
        {
            if (cancellation.IsCancellationRequested) throw;
            var diagnostic = ex is ProviderFailureException failure ? failure.Diagnostic
                : new ProviderDiagnostic(ProviderFailures.Classify(ex, cancellation), null, 0, Guid.NewGuid().ToString("N"));
            diagnose(diagnostic);
            logger?.LogWarning(new EventId(4101, "AdvisorFallback"), "Advisor deterministic fallback used: attempt={AttemptId} reason={Outcome}", diagnostic.AttemptId, diagnostic.Outcome);
            plan = await demo.PlanAsync(request.Text, c.State, cancellation); mode = "deterministic-fallback";
        }
        // Native capability plans bypass all legacy phrase routing. Server entitlement and confirmation guards above still apply.
        if (plan.Commands is { } commands)
        {
            var goal = plan.Goal;
            // Typed legacy integrations can also state an explicit requested outcome independently of steps.
            if (goal is null && plan.Intent == "compare" && plan.VehicleQuery is { } target && plan.Facts is null && !plan.NeedsRepresentative)
                goal = new("comparison", new("calculate_vehicle_comparison", System.Text.Json.JsonSerializer.SerializeToElement(new { vehicles = new[] { target } })));
            return new CapabilityOrchestrator(data, tools, this).Execute(c, commands, mode, goal);
        }
        if (provider.UsesNativeCapabilities && LegacyGroupClaim(c, request.Text) is { } compatibilityClaim)
            return compatibilityClaim;
        if (plan.Intent == "clarify_domain")
        {
            if (request.Facts is not null) plan = plan with { Intent = c.State.Intent };
            else return new(Message("assistant", "לא הצלחתי להבין את הבקשה באופן שמאפשר בדיקה בטוחה. אפשר לבחור רכב מהקטלוג, להזין נתונים בטופס או לבקש השוואה, זכאות או נציג. הנתונים והפנייה הקיימת לא שונו.", mode));
        }
        // Compatibility/offline routing only. Production tool plans do not depend on these phrases.
        if (IntentHints.ElectricComparison(request.Text) && !IntentHints.Representative(request.Text, c.State.PendingField))
        {
            var mentioned = VehicleSearch.Mentions(data.Catalog.Values, request.Text)
                .Where(id => data.Catalog[id].Drive == data.Rules.Policy.DriveCodes["3"]).ToArray();
            plan = plan with { Intent = "electric", VehicleQuery = mentioned.Length == 1 ? mentioned[0] : mentioned.Length > 1 ? request.Text : null,
                NeedsRepresentative = false, GroupQuery = null };
        }
        else if (IntentHints.ElectricOptions(request.Text))
            plan = new("electric_options", GroupQuery: IntentHints.Group(request.Text));
        if (plan.Intent == "electric_options") return ElectricOptions(c.State, plan.GroupQuery, mode);
        var s = c.State; var pending = s.PendingField;
        var patch = request.Facts ?? plan.Facts;
        var confirmed = s.ProposedFacts is not null && Confirmation(request.Text);
        if (confirmed) { patch = s.ProposedFacts; s.ProposedFacts = null; s.ProposedPlan = null; }
        if (s.VerifiedGroup is not null && patch?.Occupation is { } extractedOccupation
            && extractedOccupation != data.Employees[c.ProfileKey].Occupation)
        {
            // A model cannot replace authoritative occupation/entitlement, even via confirmation.
            patch = patch with { Occupation = null };
            plan = plan with { Facts = patch };
        }
        if (s.VerifiedGroup is not null && patch?.EligibleChildren is { } claimedChildren
            && claimedChildren != data.Employees[c.ProfileKey].EligibleChildren)
            return new(Message("assistant", "מספר הילדים שבזכאות לא שונה לפי טענה. יש לברר את הפער עם נציג; אפשר להמשיך בחלופות שבמסגרת הפרופיל המיובא.", mode));
        if (!confirmed && mode == "llm" && request.Facts is null && patch is not null && Merge(s.Facts, patch) != s.Facts)
        {
            AdvisorTools.Validate(patch); s.ProposedFacts = patch; s.ProposedPlan = plan; if (s.PendingTicketId is null) s.Stage = "ConfirmingFacts";
            return new(Message("assistant", "זוהו נתונים חדשים מהודעתך. יש לבדוק אותם בכרטיס האישור וללחוץ על אישור הנתונים לפני חישוב. אפשר לתקן בטופס.", mode));
        }
        if (patch is not null)
        {
            var previous = s.Facts; s.Facts = Merge(s.Facts, patch); s.ProposedFacts = null; s.ProposedPlan = null;
            if (patch.CurrentCar is { } changed && changed != previous.CurrentCar && patch.CurrentParticipation is null) s.Facts = s.Facts with { CurrentParticipation = null };
            if (s.Facts.CurrentCar == "waiver")
            {
                if (patch.CurrentParticipation is > 0) throw new AdvisorException(422, "במסלול ויתור אין השתתפות ברכב; יש להזין 0.");
                s.Facts = s.Facts with { CurrentParticipation = 0 };
            }
        }
        AdvisorTools.Validate(s.Facts);
        var trace = new List<ToolInvocation>(); string? superseded = null;
        if (s.Facts.CurrentCar is { } current && current != "waiver" && !data.Catalog.ContainsKey(current))
        {
            var matches = tools.LookupVehicle(new(current)); trace.Add(new("lookup_vehicle", matches.Select(v => v.Id).ToArray()));
            s.Facts = s.Facts with { CurrentCar = matches.Length == 1 ? matches[0].Id : null };
        }
        if ((mode is "confirmed-input" or "structured-input") && s.PendingCapability is { } continuation)
        {
            s.PendingCapability = null;
            if (continuation.Name is "get_eligible_vehicles" or "calculate_vehicle_comparison" or "explain_calculation" or "validate_eligibility")
                return new CapabilityOrchestrator(data, tools, this).Execute(c, [continuation], mode);
        }
        if (plan.VehicleQuery is { } query && pending != "currentCar")
        {
            var choice = query == "waiver" ? "waiver" : Resolve(query, trace);
            if (choice is null)
            {
                if (s.PendingTicketId is null) s.Stage = "SelectingAlternative";
                return new(Message("assistant", "לא זוהה רכב יחיד מהמקור. יש לבחור מזהה רכב מדויק מהקטלוג; לא נחשב על רכב קודם במקום הבחירה החדשה.", mode, traces: trace));
            }
            if (choice != s.Alternative)
            {
                if (s.PendingTicketId is not null) return PendingSelection(mode);
                superseded = s.PendingTicketId; s.PendingTicketId = null; s.ConfirmedParticipation = null; s.ConfirmedVehicle = null; s.Alternative = choice;
            }
        }
        if (plan.VehicleQuery is null && (plan.Intent is "electric" or "family") && s.Alternative is { } selected)
        {
            var matches = data.Catalog.TryGetValue(selected, out var selectedCar) && (plan.Intent == "family"
                ? data.Rules.Policy.Family.Cars.Contains(selected) : selectedCar.Drive == data.Rules.Policy.DriveCodes["3"]);
            if (!matches) { if (s.PendingTicketId is not null) return PendingSelection(mode); superseded ??= s.PendingTicketId; s.PendingTicketId = null; s.Alternative = null; s.ConfirmedParticipation = null; s.ConfirmedVehicle = null; }
        }
        if (plan.Intent == "waiver" && pending != "currentCar")
        {
            if (s.PendingTicketId is not null && s.Alternative != "waiver") return PendingSelection(mode);
            if (s.Alternative != "waiver") { superseded ??= s.PendingTicketId; s.PendingTicketId = null; s.ConfirmedParticipation = null; s.ConfirmedVehicle = null; }
            s.Alternative = "waiver";
        }
        if (s.PendingTicketId is null) s.Intent = plan.Intent;
        if (s.PendingTicketId is not null)
        { s.Stage = s.Stage == "AwaitingClarification" ? s.Stage : "WaitingForRepresentative"; return new(Message("assistant", "הפנייה עדיין בטיפול נציג. הנתונים המעודכנים נשמרו; נמשיך באותו שלב אחרי תשובה. אם התבקשה הבהרה, יש לשלוח אותה בשדה השיתוף עם הנציג.", mode, traces: trace)); }
        if (plan.NeedsRepresentative)
        {
            if (s.ProposedHandoff is not null)
                return new(Message("assistant", "כבר קיימת פעולה שממתינה לאישורך בכרטיס בשיחה. היא לא בוצעה עדיין; יש לאשר אותה בכרטיס או לבקש לבטל את ההצעה.", mode));
            // Portfolio uses the same explicit handoff confirmation in every planning mode.
            return new CapabilityOrchestrator(data, tools, this).Execute(c,
                [new("create_representative_request", System.Text.Json.JsonSerializer.SerializeToElement(new { category = plan.QuestionCategory ?? "BusinessQuestion" }))], mode);
        }
        return Continue(c, mode, trace) with { SupersededTicket = superseded };
    }
    private AgentOutcome? LegacyGroupClaim(Conversation c, string text)
    {
        var claimedGroup = IntentHints.CanonicalGroup(IntentHints.Group(text));
        if (c.State.VerifiedGroup is { } verified && IntentHints.GroupClaim(text) && claimedGroup != verified)
        {
            c.State.Claims["group"] = claimedGroup ?? "";
            c.State.Evidence["GroupClaim"] = new("employee_claim", "conflicts_verified_profile");
            return GroupConflict(verified, claimedGroup);
        }
        if (c.State.VerifiedGroup is null && c.State.Facts.Occupation is null && IntentHints.GroupClaim(text))
        {
            c.State.Stage = "GatheringFacts"; c.State.PendingField = "occupation";
            return new(Message("assistant", "זו שיחה בפרופיל אישי נפרד, ולא בפרופיל העובד הפעיל. קבוצה שנכתבה בשיחה אינה מספיקה לאימות זכאות. "
                + "כדי להשתמש בנתוני העובד שכבר מוצגים, בחרו ״עובד ההדגמה הפעיל״. לחלופין, בחרו פעם אחת עיסוק מהרשימה לפרופיל האישי.", "application"));
        }
        return null;
    }
    private string? Resolve(string query, List<ToolInvocation> trace)
    {
        var matches = tools.LookupVehicle(new(query)); trace.Add(new("lookup_vehicle", matches.Select(v => v.Id).ToArray()));
        return matches.Length == 1 ? matches[0].Id : null;
    }
    private static AgentOutcome PendingSelection(string mode) => new(Message("assistant",
        "הפנייה הקיימת נשארה פתוחה והרכב לא הוחלף. כדי לבחור חלופה אחרת, בקשו לסגור את הפנייה בכפתור ואשרו את הסגירה. נתונים מעודכנים נשמרו ללא אישור כספי.", mode));
    private void RestoreVerifiedContext(Conversation c)
    {
        if (!data.Employees.TryGetValue(c.ProfileKey, out var employee)) { c.State.VerifiedGroup = null; return; }
        var baseline = Initial(c.ProfileKey).Facts;
        var current = c.State.Facts;
        var restored = Merge(baseline, current) with { Occupation = employee.Occupation, EligibleChildren = employee.EligibleChildren };
        // A changed current car needs its own participation; never borrow the old car's amount.
        if (restored.CurrentCar != baseline.CurrentCar && current.CurrentParticipation is null)
            restored = restored with { CurrentParticipation = null };
        c.State.Facts = restored;
        c.State.VerifiedGroup = employee.Group;
        c.State.Evidence["VerifiedGroup"] = new("verified_import", "usable");
        var known = c.State.PendingField switch
        {
            "occupation" => restored.Occupation is not null, "baseSalary" => restored.BaseSalary.HasValue,
            "commission" => restored.Commission.HasValue, "bonus" => restored.Bonus.HasValue,
            "pensionPercent" => restored.PensionPercent.HasValue, "studyFundPercent" => restored.StudyFundPercent.HasValue,
            "creditPoints" => restored.CreditPoints.HasValue, "eligibleChildren" => restored.EligibleChildren.HasValue,
            "currentCar" => restored.CurrentCar is not null, "currentParticipation" => restored.CurrentParticipation.HasValue, _ => false
        };
        if (known) c.State.PendingField = null;
    }
    private static AgentOutcome GroupConflict(string verified, string? claimed = null) => new(Message("assistant",
        $"לפי פרופיל העובד הפעיל, הקבוצה המאומתת היא {verified}. "
        + (claimed is null ? "שינוי העיסוק אינו תואם לפרופיל המאומת. " : $"ציינת קבוצה {claimed}, שאינה תואמת לפרופיל. ")
        + "לא נשנה את הזכאות לפי הודעה בשיחה. "
        + "אפשר להמשיך להשוואת רכב חשמלי במסגרת הזכאות הקיימת. אם הפרופיל אינו מעודכן, יש לברר זאת עם נציג; אפשר גם לבחור במפורש פרופיל אישי נפרד להדגמה.", "verified-profile"));
    private AgentOutcome ElectricOptions(ConversationState state, string? requestedGroup, string mode)
    {
        var occupation = state.Facts.Occupation is { } name && data.Occupations.TryGetValue(name, out var policy) ? policy : null;
        var group = requestedGroup?.ToUpperInvariant() ?? occupation?.Group;
        // Search aliases only: fictional group names, order, budgets and employee entitlement remain authoritative.
        group = IntentHints.CanonicalGroup(group);
        if (group is null) return new(Message("assistant", "לאיזו קבוצה לבדוק רכבים חשמליים? אפשר לציין קבוצה או לבחור עיסוק מהרשימה הפיקטיבית.", mode));
        var sample = data.Occupations.Values.FirstOrDefault(o => o.Group == group);
        if (sample is null) return new(Message("assistant", "הקבוצה אינה קיימת במקור. הקבוצות הזמינות: " + string.Join(", ", data.Rules.Policy.GroupOrder), mode));
        var facts = new EmployeeFacts(Occupation: sample.Name, EligibleChildren: state.Facts.EligibleChildren);
        var cars = data.Catalog.Values.Where(v => v.Drive == data.Rules.Policy.DriveCodes["3"])
            .Where(v => tools.CheckEligibility(new(facts, v.Id)).Eligible).OrderBy(v => v.Id).ToArray();
        var label = requestedGroup is not null && requestedGroup != group ? $"{requestedGroup} ({group})" : group;
        var text = $"רכבים חשמליים במסגרת קבוצה {label}, לפי הקבוצה והתקציב הפיקטיביים: "
            + (cars.Length == 0 ? "לא נמצאו רכבים מתאימים." : string.Join(" · ", cars.Select(v => $"{v.Id}: {v.CommercialName}")))
            + " זו בדיקת אפשרויות בלבד; הקבוצה, הפרופיל והמסלול שלך לא שונו. לבדיקת רכב מסוים בחרו מזהה מהקטלוג; הזכאות בפועל תיבדק לפי העיסוק שלך.";
        return new(Message("assistant", text, mode, traces: [new("lookup_vehicle", cars.Select(v => v.Id).ToArray()), new("check_eligibility", cars.Select(v => v.Id).ToArray())], sources: Rules()));
    }
    public AgentOutcome Continue(Conversation c, string? mode = null, List<ToolInvocation>? trace = null)
    {
        mode ??= Mode; trace ??= []; var s = c.State;
        AgentOutcome Ask(string field, string text) { s.PendingField = field; s.Stage = "GatheringFacts"; return new(Message("assistant", text, mode, traces: trace)); }
        if (s.Facts.Occupation is null || !data.Occupations.ContainsKey(s.Facts.Occupation)) return Ask("occupation", "מה העיסוק שלך? יש לבחור עיסוק מהרשימה הפיקטיבית כדי לגזור זכאות, בלי לנחש קבוצה.");
        if (s.Intent != "breakdown" && s.Alternative is null)
        {
            s.Stage = "SelectingAlternative"; s.PendingField = null;
            var candidates = data.Catalog.Values.Where(v => s.Intent != "electric" || v.Drive == data.Rules.Policy.DriveCodes["3"])
                .Where(v => s.Intent != "family" || data.Rules.Policy.Family.Cars.Contains(v.Id))
                .Where(v => s.Intent != "electric" || tools.CheckEligibility(new(s.Facts, v.Id)).Eligible).ToArray();
            trace.Add(new("lookup_vehicle", candidates.Select(v => v.Id).ToArray()));
            return new(Message("assistant", (s.Intent == "electric" && s.VerifiedGroup is not null
                ? "נתוני הפרופיל הפעיל כבר זמינים. אלה הרכבים החשמליים שבזכאותך; איזה מהם להשוות לרכב הנוכחי? "
                : "איזה רכב תרצה/י לבדוק? בחרו מהרשימה: ")
                + (candidates.Length == 0 ? "לא נמצאו חלופות מתאימות לפי הזכאות הקיימת." : string.Join(" · ", candidates.Select(v => $"{v.Id}: {v.CommercialName}"))), mode, traces: trace));
        }
        var route = s.Intent == "breakdown" ? s.Facts.CurrentCar : s.Alternative;
        if ((route is not null && data.Rules.Policy.Family.Cars.Contains(route) || s.Intent != "eligibility" && s.Facts.CurrentCar is { } currentCar && data.Rules.Policy.Family.Cars.Contains(currentCar)) && s.Facts.EligibleChildren is null)
            return Ask("eligibleChildren", $"כמה ילדים יש לך עד גיל {data.Rules.Policy.Family.ChildMaxAge}? אין להסיק זאת מנקודות הזיכוי.");
        if (s.Intent != "breakdown" && route is not null)
        {
            BenefitEligibility eligibility;
            try { eligibility = tools.CheckEligibility(new(s.Facts, route)); trace.Add(new("check_eligibility", [s.Facts.Occupation, route])); }
            catch (CalculationException ex) { return Ask("alternative", ex.Message); }
            if (!eligibility.Eligible)
            {
                s.Stage = "SelectingAlternative"; s.Alternative = null;
                var explanation = route == "waiver" ? "לפי נתוני התפקיד המאומתים שלך, התפקיד מחייב רכב חברה ולכן לא ניתן לוותר עליו ולקבל במקומו אחזקת רכב. אם נתוני התפקיד אינם מעודכנים, אפשר לפנות לנציג לבירור."
                    : data.Rules.Policy.Family.Cars.Contains(route) ? $"המסלול המשפחתי דורש לפחות {data.Rules.Policy.Family.MinChildren} ילדים עד גיל {data.Rules.Policy.Family.ChildMaxAge}."
                    : "הרכב מחוץ לקבוצת הזכאות או לתקציב המותר לפי המקור.";
                return new(Message("assistant", explanation + " בחרו חלופה אחרת; נציג או מודל אינם יכולים לעקוף את הכללים.", mode, traces: trace, sources: Rules()));
            }
            if (s.Intent == "eligibility") { s.Stage = "Completed"; s.PendingField = null; return new(Message("assistant", "החלופה בזכאות לפי העיסוק והכללים הפיקטיביים. התקציב: " + Money(eligibility.Budget) + ". להערכת נטו אפשר לבקש השוואה; לא נדרש שכר לבדיקת זכאות בלבד.", mode, traces: trace, sources: Sources(route))); }
        }
        foreach (var (missing, question) in new (bool, (string, string))[]{
            (s.Facts.BaseSalary is null,("baseSalary","מה שכר הבסיס החודשי ברוטו?")),
            (s.Facts.Commission is null,("commission","מה סכום העמלות החודשי? יש להזין 0 במפורש אם אין.")),
            (s.Facts.Bonus is null,("bonus","מה סכום הבונוס החודשי? יש להזין 0 במפורש אם אין.")),
            (s.Facts.PensionPercent is null,("pensionPercent","מה אחוז פנסיית העובד? למשל ערך אחוזי, לא סכום כסף.")),
            (s.Facts.StudyFundPercent is null,("studyFundPercent","מה אחוז קרן ההשתלמות של העובד? 0 אם אין.")),
            (s.Facts.CreditPoints is null,("creditPoints","כמה נקודות זיכוי יש לך?")),
            (s.Facts.CurrentCar is null,("currentCar","מה הרכב הנוכחי? בחרו מזהה מהקטלוג או ויתור אם אין רכב חברה.")),
            (s.Facts.CurrentCar is not(null or "waiver")&&s.Facts.CurrentParticipation is null,("currentParticipation","מה ההשתתפות הנוכחית שלך ברכב, כניכוי נטו? אין לנחש סכום זה."))})
            if (missing) return Ask(question.Item1, question.Item2);
        s.PendingField = null;
        try
        {
            if (s.Intent == "breakdown")
            {
                var result = tools.CalculateNet(new(s.Facts, s.Facts.CurrentCar!, s.Facts.CurrentCar == "waiver" ? 0 : s.Facts.CurrentParticipation)); trace.Add(new("calculate_net", [s.Facts.CurrentCar!])); s.Stage = "Completed";
                return new(Message("assistant", "הנטו החודשי המשוער במסלול הנוכחי: " + Money(result.Salary.DisplayNetSalary) + ". פירוט הניכויים מוצג מתוך מנוע החישוב, לא מתוך המודל.", mode, breakdown: result.Salary, traces: trace, sources: Sources(s.Facts.CurrentCar!)) with {
                    Baseline = EmployeeExperience.Present("private-session", new SalaryComparison(result, result), data).Current });
            }
            var resultComparison = tools.CompareScenarios(new(s.Facts, s.Alternative!, s.ConfirmedVehicle == s.Alternative ? s.ConfirmedParticipation : null));
            trace.Add(new("calculate_net", [s.Facts.CurrentCar!, s.Alternative!])); trace.Add(new("compare_scenarios", [s.Alternative!])); s.Stage = "Completed";
            var presentation = EmployeeExperience.Present("private-session", resultComparison, data, "conversation_inputs", s.ConfirmedVehicle == s.Alternative && s.ConfirmedParticipation.HasValue ? "representative_approval" : "catalog_default");
            s.RecentComparisons = [s.Alternative!];
            var a = resultComparison.Current.Salary; var b = resultComparison.Alternative.Salary;
            var text = $"היום: {Money(a.DisplayNetSalary)} נטו. בחלופה: {Money(b.DisplayNetSalary)}. ההפרש המוצג: {Money(resultComparison.DisplayNetDifference)} לחודש. "
                + $"שווי השימוש משתנה מ־{Money(resultComparison.Current.Scenario.Car?.MonthlyTaxableValueOfUse ?? 0)} ל־{Money(resultComparison.Alternative.Scenario.Car?.MonthlyTaxableValueOfUse ?? 0)}; הוא משפיע על מס וביטוח ואינו ניכוי מזומן נוסף. "
                + $"ההשתתפות הכוללת משתנה מ־{Money(a.EmployeeNetParticipation)} ל־{Money(b.EmployeeNetParticipation)}. "
                + (resultComparison.Alternative.Scenario.Car is null
                    ? $"בוויתור על רכב נוספה אחזקת רכב חייבת במס בסך {Money(resultComparison.Alternative.Scenario.TaxableMaintenanceAllowance)}. האחזקה אינה מגדילה את השכר הפנסיוני; שווי השימוש וההשתתפות ברכב הוסרו. " : "")
                + (data.Rules.Policy.Family.Cars.Contains(s.Alternative!)
                    ? $"במסלול המשפחתי העלות החודשית היא {Money(resultComparison.Alternative.Eligibility.MonthlyCost)}, מול תקציב {Money(resultComparison.Alternative.Eligibility.Budget)}. "
                        + $"חלק העובד בפער התקציב: {Money(resultComparison.Alternative.Eligibility.FamilyEmployeeShare)}; חלק המעסיק: {Money(resultComparison.Alternative.Eligibility.FamilyEmployerShare)}. "
                        + "חלק העובד כבר כלול בהשתתפות הכוללת ואינו מנוכה פעמיים. " : "")
                + (resultComparison.Alternative.Scenario.IncludeElectricReimbursement ? $"החזר החשמל מגולם לתוספת מדויקת של {Money(data.Rules.Business.ElectricReimbursementNetTarget)} נטו. " : "")
                + $"החישוב לפי לוחות {data.Rules.PayrollTaxYear} של הפורטפוליו; אין עיגול במהלך חישוב השכר.";
            return new(Message("assistant", text, mode, presentation, traces: trace, sources: Sources(s.Alternative!)));
        }
        catch (CalculationException ex) { s.Stage = "GatheringFacts"; return new(Message("assistant", "לא ניתן לחשב: " + ex.Message + " יש לתקן את הנתונים או לבחור מסלול מותר.", mode, traces: trace, sources: Rules())); }
    }
    private static string Money(decimal amount) => amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("he-IL")) + " ₪";
    private static void UpdateEvidence(ConversationState state, MessageRequest request, EmployeeFacts before)
    {
        if (state.ConfirmedParticipation is null) state.Evidence.Remove("AlternativeParticipation");
        var previous = System.Text.Json.JsonSerializer.SerializeToElement(before);
        var proposals = System.Text.Json.JsonSerializer.SerializeToElement(state.ProposedFacts);
        foreach (var field in System.Text.Json.JsonSerializer.SerializeToElement(state.Facts).EnumerateObject())
        {
            if (proposals.ValueKind == System.Text.Json.JsonValueKind.Object && proposals.TryGetProperty(field.Name, out var proposal) && proposal.ValueKind != System.Text.Json.JsonValueKind.Null)
                state.Evidence[field.Name] = new("model_suggestion", "awaiting_confirmation");
            else if (field.Value.ValueKind == System.Text.Json.JsonValueKind.Null) state.Evidence[field.Name] = new("none", "missing");
            else if (field.Value.GetRawText() != previous.GetProperty(field.Name).GetRawText())
                state.Evidence[field.Name] = new(request.Facts is not null ? "structured_employee_input" : state.Evidence.GetValueOrDefault(field.Name)?.Status == "awaiting_confirmation" ? "employee_confirmed" : "server_context", "usable");
            else if (!state.Evidence.ContainsKey(field.Name) || state.Evidence[field.Name].Status == "awaiting_confirmation" && state.ProposedFacts is null)
                state.Evidence[field.Name] = new(state.VerifiedGroup is null ? "employee_input" : "verified_profile_snapshot", "usable");
        }
    }
    public static EmployeeFacts Merge(EmployeeFacts a, EmployeeFacts b) => new(b.Occupation ?? a.Occupation, b.BaseSalary ?? a.BaseSalary, b.Commission ?? a.Commission, b.Bonus ?? a.Bonus, b.PensionPercent ?? a.PensionPercent, b.StudyFundPercent ?? a.StudyFundPercent, b.CreditPoints ?? a.CreditPoints, b.EligibleChildren ?? a.EligibleChildren, b.CurrentCar ?? a.CurrentCar, b.CurrentParticipation ?? a.CurrentParticipation);
    private static SourceLink[] Rules() => [new("כללי הפורטפוליו — מס, זכאות וגילום", "/api/advisor/sources/rules")];
    private SourceLink[] Sources(string choice) => Rules().Concat(choice == "waiver" ? [] : new[] { new SourceLink($"רשומת הרכב {choice} — מפתח מלא ושווי שימוש", $"/api/advisor/sources/vehicles/{choice}") }).ToArray();
}
