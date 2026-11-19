using System.Globalization;
using System.Text.Json;
using StraussDrive.Domain;
using StraussDrive.Import;

namespace StraussDrive.Api.Advisor;

/// <summary>Bounded, authorized execution. Model arguments never contain identities or approved amounts.</summary>
public sealed class CapabilityOrchestrator(HackathonData data, IAdvisorTools tools, ConversationAgent agent)
{
    public AgentOutcome Execute(Conversation conversation, AdvisorCommand[] commands, string mode, AdvisorGoal? goal = null)
    {
        var state = conversation.State;
        CapabilityArguments[] arguments;
        AdvisorCommand? completion;
        try
        {
            completion = AdvisorGoals.Completion(goal);
            if (commands.Length is < 1 or > 6) throw new InvalidOperationException();
            var validated = commands.Select(command => (Command: command, Arguments: AdvisorCapabilities.Validate(command))).ToArray();
            // A model may repeat facts that are already usable in the server-owned context.
            // Validate every original command first; remove only exact no-op proposals before
            // enforcing exclusive mutations. Unknown or genuinely changed facts stay guarded.
            var effective = validated.Where(call => call.Command.Name != "propose_fact_changes"
                || !RepeatsKnownFacts(state, call.Arguments.Facts!)).ToArray();
            if (effective.Length == 0 && completion is null) effective = [validated[0]];
            if (goal is { Kind: not "comparison" } && effective.Any(call => call.Command.Name == "calculate_vehicle_comparison"))
                throw new InvalidOperationException("Calculation conflicts with requested outcome.");
            commands = AdvisorGoals.Complete(effective.Select(call => call.Command).ToArray(), completion);
            arguments = commands.Select(AdvisorCapabilities.Validate).ToArray();
            if (commands.Length > 1 && commands.Any(c => c.Name is "propose_fact_changes" or "create_representative_request" or "cancel_representative_request" or "discard_proposals"))
                throw new InvalidOperationException();
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or AdvisorException)
        { return new(ConversationAgent.Message("assistant", "לא הצלחתי לבצע את הבקשה בבטחה. אפשר לבחור רכב מהקטלוג, לבקש הסבר לכללים או לנסח את הבקשה מחדש.", mode)); }
        var messages = new List<ConversationMessage>();
        var attempted = new List<ComparisonSummaryRow>();
        var comparedIds = new HashSet<string>(StringComparer.Ordinal);
        // Continue mutates recent state per vehicle; retain the prior turn for follow-up resolution.
        var recent = state.RecentComparisons.ToArray();
        var approvedVehicle = state.ConfirmedVehicle;
        var approvedParticipation = state.ConfirmedParticipation;
        var recommend = arguments.Any(a => a.Recommend == true);
        var tableOnly = arguments.Any(a => a.TableOnly == true);
        // Read-only detours preserve the unfinished task; a replacement task explicitly supersedes it.
        if (commands.Any(command => command.Name is "get_eligible_vehicles" or "calculate_vehicle_comparison" or "validate_eligibility" or "discard_proposals")) state.PendingCapability = null;
        for (var index = 0; index < commands.Length; index++)
        {
            var command = commands[index]; var args = arguments[index];
            try
            {
                switch (command.Name)
                {
                    case "get_active_employee":
                        messages.Add(Reply(state.VerifiedGroup is { } group
                            ? $"נתוני פרופיל העובד הפעיל זמינים. הקבוצה המאומתת היא {group}; הרכב הנוכחי הוא {(state.Facts.CurrentCar is { } current ? Label(current) : "טרם נבחר")}. נשתמש בנתונים הקיימים ונבקש רק מידע שחסר."
                            : "זו שיחה בפרופיל אישי. נשתמש בנתונים שהזנת ונבקש רק מידע שחסר.", command.Name));
                        break;
                    case "get_eligible_vehicles":
                        if (state.Facts.Occupation is null || !data.Occupations.ContainsKey(state.Facts.Occupation)) { state.PendingCapability = command; messages.Add(agent.Continue(conversation, mode).Message); break; }
                        if (args.Drive is { } drive && drive != state.Preference && state.PendingTicketId is null)
                        { state.Alternative = null; state.ConfirmedParticipation = null; state.ConfirmedVehicle = null; state.ProposedHandoff = null; }
                        state.Preference = args.Drive ?? state.Preference;
                        state.CandidateSort = args.Sort ?? state.CandidateSort;
                        if (state.Preference == "family" && state.Facts.EligibleChildren is null)
                        {
                            state.PendingField = "eligibleChildren";
                            if (state.PendingTicketId is null) state.Stage = "GatheringFacts";
                            state.PendingCapability = command;
                            messages.Add(Reply($"לבדיקת המסלול המשפחתי חסר רק מספר הילדים עד גיל {data.Rules.Policy.Family.ChildMaxAge}. כמה ילדים עומדים בתנאי הזה?", command.Name)); break;
                        }
                        var cars = data.Catalog.Values.Where(v => state.Preference != "electric" || v.Drive == data.Rules.Policy.DriveCodes["3"])
                            .Where(v => state.Preference != "family" || data.Rules.Policy.Family.Cars.Contains(v.Id))
                            .Where(v => !data.Rules.Policy.Family.Cars.Contains(v.Id) || state.Facts.EligibleChildren.HasValue)
                            .Where(v => tools.CheckEligibility(new(state.Facts, v.Id)).Eligible)
                            .OrderBy(v => state.CandidateSort == "monthly_cost" ? v.MonthlyCost : 0).ThenBy(v => v.Id).ToArray();
                        state.Candidates = cars.Select(v => v.Id).ToArray();
                        state.PendingField = null;
                        if (state.PendingTicketId is null) state.Stage = "SelectingAlternative";
                        messages.Add(Reply(cars.Length == 0 ? "לא נמצאו רכבים מתאימים לנתונים ולהעדפות הקיימים. אפשר לשנות העדפות או להשלים נתונים."
                            : "אלה הרכבים שבזכאותך לפי הכללים הפיקטיביים: " + string.Join(" · ", cars.Select((v, i) => $"{i + 1}. {v.CommercialName} ({v.Id}), עלות חודשית {v.MonthlyCost.ToString("N0", CultureInfo.GetCultureInfo("he-IL"))} ₪"))
                                + ". אפשר לבחור רכב, להשוות כמה חלופות או לשנות העדפה.", command.Name, state.Candidates));
                        break;
                    case "calculate_vehicle_comparison":
                    case "validate_eligibility":
                        var selections = Select(state, args, recent);
                        if (selections.Length == 0)
                        { messages.Add(Reply("איזה רכב להשוות? אפשר לבחור לפי שם, מזהה או מיקום ברשימה האחרונה. אפשר גם לבדוק ויתור על רכב.", command.Name, status: "needs_input")); break; }
                        if (state.PendingTicketId is not null && command.Name == "calculate_vehicle_comparison")
                        { messages.Add(Reply("הפנייה הקיימת עדיין בטיפול. אפשר לבדוק כללים ואפשרויות. כדי להחליף את הרכב בהשוואה, אפשר לבקש לסגור תחילה את הפנייה הקיימת; הסגירה תדרוש את אישורך.", command.Name, status: "waiting_for_representative")); break; }
                        if (command.Name == "calculate_vehicle_comparison")
                            foreach (var selection in selections)
                                if (!attempted.Any(r => r.VehicleId == selection))
                                    attempted.Add(new(selection, Label(selection), false, "unavailable", "לא ניתן לחשב לפי הנתונים הקיימים; יש להשלים את המידע המבוקש"));
                        foreach (var selection in selections)
                        {
                            if (command.Name == "calculate_vehicle_comparison" && selection == "current")
                            {
                                attempted.RemoveAll(row => row.VehicleId == "current");
                                if (!comparedIds.Add("current")) continue;
                                if (state.ProposedFacts is not null)
                                { messages.Add(Reply("יש שינוי נתונים שממתין לאישור. אשרו או בטלו אותו לפני החישוב.", command.Name, status: "needs_input")); break; }
                                var intent = state.Intent;
                                state.Intent = "breakdown";
                                ConversationMessage currentMessage;
                                try { currentMessage = agent.Continue(conversation, mode).Message; }
                                finally { state.Intent = intent; }
                                if (state.PendingField is not null) state.PendingCapability = command;
                                messages.Add(currentMessage);
                                continue;
                            }
                            if (command.Name == "validate_eligibility")
                            {
                                var missing = state.Facts.Occupation is null || !data.Occupations.ContainsKey(state.Facts.Occupation) ? "occupation"
                                    : data.Rules.Policy.Family.Cars.Contains(selection) && state.Facts.EligibleChildren is null ? "eligibleChildren" : null;
                                if (missing is not null)
                                {
                                    state.PendingField = missing;
                                    if (state.PendingTicketId is null) state.Stage = "GatheringFacts";
                                    state.PendingCapability = new(command.Name, JsonSerializer.SerializeToElement(new { vehicles = selections }));
                                    messages.Add(Reply(missing == "occupation" ? "מה העיסוק שלך? יש לבחור מהרשימה הפיקטיבית לבדיקת זכאות."
                                        : $"כמה ילדים יש לך עד גיל {data.Rules.Policy.Family.ChildMaxAge}? המידע נדרש לבדיקת המסלול המשפחתי בלבד.", command.Name, status: "needs_input"));
                                    break;
                                }
                                var check = tools.CheckEligibility(new(state.Facts, selection));
                                state.PendingField = null;
                                if (state.PendingTicketId is null) state.Stage = "Completed";
                                messages.Add(Reply(check.Eligible ? $"{Label(selection)} במסגרת הזכאות לפי העיסוק והכללים הפיקטיביים."
                                    : $"{Label(selection)} אינו במסגרת הזכאות לפי הנתונים והכללים הקיימים. אפשר לבחור חלופה מהרשימה המתאימה.", command.Name, [selection]));
                            }
                            else
                            {
                                // Canonical IDs, across every model call in this turn, before invoking the engine.
                                if (!comparedIds.Add(selection)) continue;
                                var rowIndex = attempted.FindIndex(r => r.VehicleId == selection);
                                if (selection != "waiver" && !data.Catalog.ContainsKey(selection))
                                {
                                    attempted[rowIndex] = attempted[rowIndex] with { Explanation = "הרכב לא זוהה באופן חד־משמעי; בחרו מזהה מדויק מהקטלוג" };
                                    messages.Add(Reply(attempted[rowIndex].Explanation, command.Name, [selection], "needs_input"));
                                    continue;
                                }
                                if (state.ProposedFacts is not null)
                                {
                                    attempted[rowIndex] = attempted[rowIndex] with { Status = "awaiting_confirmation", Explanation = "יש שינוי נתונים שממתין לאישור. אפשר לאשר או לבטל אותו לפני השוואה חדשה." };
                                    messages.Add(Reply(attempted[rowIndex].Explanation, command.Name)); break;
                                }
                                if (state.Alternative != selection) { state.ConfirmedParticipation = null; state.ConfirmedVehicle = null; state.ProposedHandoff = null; }
                                if (selection == approvedVehicle)
                                { state.ConfirmedVehicle = approvedVehicle; state.ConfirmedParticipation = approvedParticipation; }
                                state.Alternative = selection; state.Intent = "compare";
                                ConversationMessage compared;
                                try { compared = agent.Continue(conversation, mode).Message; }
                                catch (Exception ex) when (ex is CalculationException or AdvisorException or InvalidOperationException or HttpRequestException or IOException or TimeoutException or OverflowException)
                                {
                                    attempted[rowIndex] = attempted[rowIndex] with { Status = "failed", Explanation = "החישוב לא הושלם; לא הונח סכום או אישור זכאות" };
                                    messages.Add(Reply(attempted[rowIndex].Explanation, command.Name, [selection], "failed"));
                                    continue;
                                }
                                if (state.Stage == "GatheringFacts" && state.PendingField is not null)
                                    state.PendingCapability = new("calculate_vehicle_comparison", JsonSerializer.SerializeToElement(args with { Vehicles = selections, Candidate = null, IncludeRecent = null },
                                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
                                if (compared.Comparison is null)
                                    attempted[rowIndex] = attempted[rowIndex] with { Explanation = compared.Text,
                                        Status = state.Stage == "SelectingAlternative" ? "ineligible" : "needs_input" };
                                messages.Add(compared with { Tools = [new(command.Name, [selection], compared.Comparison is null ? "needs_input" : "completed"), .. compared.Tools] });
                                if (state.PendingCapability is not null) break;
                            }
                        }
                        break;
                    case "explain_calculation":
                        if (state.PendingTicketId is not null)
                        {
                            messages.Add(Reply("ההשוואה ממתינה לתשובת הנציג. ההשתתפות טרם אושרה ולכן לא יוצג חישוב חלופי כאילו התקבלה תשובה. אפשר לעיין בחישוב הקודם ובכללים או להמתין לתשובה בשיחה הזו.", command.Name, status: "waiting_for_representative"));
                            break;
                        }
                        // Comparison results already carry their complete explanation and provenance.
                        if (commands.Any(c => c.Name == "calculate_vehicle_comparison")) break;
                        var previous = state.Intent; state.Intent = state.Alternative is null ? "breakdown" : "compare";
                        ConversationMessage explained;
                        try { explained = agent.Continue(conversation, mode).Message; }
                        finally { state.Intent = previous; }
                        if (state.Stage == "GatheringFacts" && state.PendingField is not null) state.PendingCapability = command;
                        messages.Add(explained with { Tools = [new(command.Name, [], explained.Breakdown is null && explained.Comparison is null ? "needs_input" : "completed"), .. explained.Tools] });
                        break;
                    case "retrieve_policy":
                        messages.Add(Reply(Policy(args.Topic ?? "electric"), command.Name));
                        break;
                    case "propose_fact_changes":
                        var facts = args.Facts!;
                        if (state.VerifiedGroup is not null && (facts.Occupation is { } occupation && occupation != state.Facts.Occupation
                            || facts.EligibleChildren is { } children && children != state.Facts.EligibleChildren))
                        { messages.Add(Reply("העיסוק, מספר הילדים והזכאות בפרופיל הפעיל מאומתים ולא שונו. טענה אינה אישור זכאות; לבירור שינוי יש לפנות לנציג.", command.Name)); break; }
                        if (RepeatsKnownFacts(state, facts))
                        { messages.Add(Reply("הנתונים האלו כבר זמינים. אפשר להמשיך בבחירת רכב או בבקשת השוואה.", command.Name)); break; }
                        state.ProposedFacts = facts; state.ProposedPlan = new(state.Intent, state.Alternative, facts);
                        if (completion is not null && state.PendingTicketId is null) state.PendingCapability = completion;
                        foreach (var field in JsonSerializer.SerializeToElement(facts).EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null))
                            state.Evidence[field.Name] = new("model_suggestion", "awaiting_confirmation");
                        if (state.PendingTicketId is null) state.Stage = "ConfirmingFacts";
                        messages.Add(Reply("זוהו נתונים חדשים. בדקו את כרטיס הנתונים ואשרו או בטלו את השינוי; עד האישור הוא לא ישמש לחישוב.", command.Name));
                        break;
                    case "create_representative_request":
                        if (state.PendingTicketId is not null)
                        { messages.Add(Reply("כבר קיימת פנייה בטיפול. התשובה וההמשך יישמרו בשיחה הזו.", command.Name, [state.PendingTicketId])); break; }
                        var category = args.Category ?? "BusinessQuestion";
                        var target = state.PendingField == "currentParticipation" ? "current" : "alternative";
                        var vehicle = target == "current" ? state.Facts.CurrentCar : state.Alternative;
                        if (category == "Participation" && vehicle is null or "waiver")
                        { messages.Add(Reply("לאישור השתתפות צריך לבחור תחילה רכב מהקטלוג. אפשר לבקש בדיקת זכאות לפני בחירה.", command.Name)); break; }
                        state.ProposedHandoff = new(AdvisorStore.Id(), category, vehicle, target);
                        messages.Add(Reply("אפשר להעביר לנציג בקשת בדיקה עם העיסוק, הקבוצה והרכב בלבד. לא נשלח את רכיבי השכר. האם לאשר את העברת הבקשה? הפנייה עדיין לא נוצרה.", command.Name));
                        break;
                    case "discard_proposals":
                        state.ProposedFacts = null; state.ProposedPlan = null; state.ProposedHandoff = null;
                        state.PendingCapability = null;
                        state.Stage = state.PendingTicketId is null ? "SelectingAlternative" : "WaitingForRepresentative";
                        messages.Add(Reply("השינויים שלא אושרו בוטלו. הנתונים המאומתים והפניות שכבר נשמרו לא שונו.", command.Name));
                        break;
                    case "cancel_representative_request":
                        if (state.PendingTicketId is null) { messages.Add(Reply("אין פנייה פתוחה לסגירה. אפשר להמשיך בבחירת רכב.", command.Name)); break; }
                        state.ProposedHandoff = new(AdvisorStore.Id(), "BusinessQuestion", state.Alternative, "alternative", "cancel", state.PendingTicketId);
                        messages.Add(Reply("אפשר לסגור את הפנייה הפתוחה כדי להמשיך בבחירה אחרת. היא עדיין לא נסגרה; נדרש אישורך לפני הסגירה.", command.Name));
                        break;
                    case "record_group_claim":
                        var claim = IntentHints.CanonicalGroup(args.Group)!; state.Claims["group"] = claim;
                        state.Evidence["GroupClaim"] = new("model_extracted_claim", claim == state.VerifiedGroup ? "matches_verified_profile" : "unverified_or_conflicting");
                        messages.Add(Reply(state.VerifiedGroup is { } verified
                            ? claim == verified ? $"הקבוצה שציינת תואמת לפרופיל המאומת: {verified}. נמשיך לפי הזכאות הקיימת."
                                : $"ציינת קבוצה {claim}, אך בפרופיל המאומת הקבוצה היא {verified}. הזכאות לא שונתה. אפשר להמשיך ברכב מתאים לפרופיל או לברר את הפער עם נציג."
                            : "הקבוצה שציינת נשמרה כטענה בלבד. בפרופיל אישי יש לבחור עיסוק מהרשימה כדי לקבוע זכאות, או לחזור לעובד ההדגמה הפעיל.", command.Name));
                        break;
                    default:
                        messages.Add(Reply("אני יכול לסייע בהטבות רכב חברה, זכאות, השוואת נטו ובירור עם נציג. בבקשה מחוץ לתחום הזה כדאי לפנות לגורם המתאים.", command.Name));
                        break;
                }
            }
            catch (Exception ex) when (ex is CalculationException or AdvisorException or InvalidOperationException or HttpRequestException or IOException or TimeoutException or OverflowException)
            {
                if (command.Name == "calculate_vehicle_comparison")
                {
                    foreach (var unresolved in args.Vehicles ?? [])
                        if (!attempted.Any(a => a.VehicleId == unresolved))
                            attempted.Add(new(unresolved, unresolved, false, "unavailable", "לא ניתן לזהות או לחשב את החלופה בבטחה; בחרו מזהה מדויק מהקטלוג"));
                }
                messages.Add(Reply("לא ניתן להשלים כרגע את הבדיקה הזו. אפשר לבדוק את הנתונים או לבחור רכב מהרשימה הפיקטיבית ולנסות שוב; לא הונחו סכומים או זכאות.", command.Name, status: "failed"));
            }
        }
        var allComparisons = messages.SelectMany(m => m.Comparisons ?? (m.Comparison is null ? [] : new[] { m.Comparison }))
            .DistinctBy(c => c.Alternative.CarId ?? "waiver").ToArray();
        var baseline = allComparisons.FirstOrDefault()?.Current ?? messages.Select(m => m.Baseline).FirstOrDefault(b => b is not null);
        var baselineTraces = new List<ToolInvocation>();
        if (baseline is null && attempted.Any(a => a.Status == "ineligible") && state.PendingTicketId is null
            && state.ProposedFacts is null && state.Facts.CurrentCar is { } currentChoice
            && (currentChoice == "waiver" || state.Facts.CurrentParticipation.HasValue))
        {
            try
            {
                var calculated = tools.CalculateNet(new(state.Facts, currentChoice, currentChoice == "waiver" ? 0 : state.Facts.CurrentParticipation));
                baseline = EmployeeExperience.Present("private-session", new SalaryComparison(calculated, calculated), data).Current;
                baselineTraces.Add(new("calculate_net", [currentChoice]));
            }
            catch (Exception ex) when (ex is CalculationException or AdvisorException or InvalidOperationException or OverflowException) { }
        }
        var unchanged = allComparisons.Where(c => ComparisonSummaries.SameScenario(c.Current, c.Alternative)).ToArray();
        var comparisons = allComparisons.Where(c => !ComparisonSummaries.SameScenario(c.Current, c.Alternative)).ToArray();
        attempted.RemoveAll(row => unchanged.Any(c => (c.Alternative.CarId ?? "waiver") == row.VehicleId));
        if (comparisons.Length > 0 && unchanged.Length > 0)
        {
            state.Alternative = comparisons[^1].Alternative.CarId ?? "waiver";
            state.ConfirmedVehicle = state.Alternative == approvedVehicle ? approvedVehicle : null;
            state.ConfirmedParticipation = state.Alternative == approvedVehicle ? approvedParticipation : null;
        }
        if (unchanged.Length > 0 && comparisons.Length == 0 && attempted.Count == 0) state.RecentComparisons = [];
        if (attempted.Count > 0 && comparisons.Length > 0)
            state.RecentComparisons = attempted.Count == 1 && recent.Length > 1 && recent.Contains(attempted[0].VehicleId)
                ? recent : attempted.Select(r => r.VehicleId).Distinct().Take(6).ToArray();
        var currentOnly = baseline is not null && comparisons.Length == 0 && attempted.Count == 0;
        var summary = (attempted.Count > 0 || allComparisons.Length > 0 || baseline is not null) && (attempted.Select(r => r.VehicleId).Distinct().Count() >= 2 || tableOnly || recommend || unchanged.Length > 0)
            ? ComparisonSummaries.Build(attempted.ToArray(), comparisons, recommend || unchanged.Length > 0, tableOnly, baseline) : null;
        var rejection = string.Join(" ", attempted.Where(a => a.Status == "ineligible").Select(a => a.Explanation).Distinct());
        var text = summary is null ? string.Join("\n\n", messages.Select(m => m.Text))
            : rejection.Length > 0 ? rejection : currentOnly
                ? "זה המסלול הנוכחי עצמו; אין שינוי בנטו ואין חלופה חדשה לבחירה."
                : "ההשוואה מבוססת על הנתונים המאומתים ומנוע החישוב. סכומים חסרים אינם מוצגים כאפס."
                + string.Join("", messages.Where(m => m.Tools.FirstOrDefault()?.Name is not ("calculate_vehicle_comparison" or "explain_calculation"))
                    .Select(m => "\n\n" + m.Text));
        return new(ConversationAgent.Message("assistant", text, mode,
            // Retain the legacy self-comparison evidence; the explicit empty alternatives array owns rendering.
            comparison: comparisons.FirstOrDefault() ?? (unchanged.Length > 0 && attempted.Count == 0 ? unchanged[0] : null), breakdown: currentOnly
                ? baseline?.Salary : messages.Select(m => m.Breakdown).FirstOrDefault(b => b is not null),
            traces: messages.SelectMany(m => m.Tools).Concat(baselineTraces), sources: messages.SelectMany(m => m.Sources).Distinct()) with {
                Comparisons = comparisons.Length > 1 || unchanged.Length > 0 || currentOnly ? comparisons : null, Summary = summary, Baseline = baseline });

        ConversationMessage Reply(string text, string tool, string[]? ids = null, string status = "completed") => ConversationAgent.Message("assistant", text, mode,
            traces: [new(tool, ids ?? [], status)], sources: [new("כללי הפורטפוליו הפיקטיביים", "/api/advisor/sources/rules")]);
    }
    private bool RepeatsKnownFacts(ConversationState state, EmployeeFacts facts)
    {
        // Treat a uniquely resolved alias of the current catalog ID as the same fact.
        // Ambiguous/unknown names and actual vehicle changes are not eligible for removal.
        if (facts.CurrentCar is { } query && state.Facts.CurrentCar is { } current && query != current)
        {
            var matches = tools.LookupVehicle(new(query));
            if (matches.Length == 1 && matches[0].Id == current) facts = facts with { CurrentCar = current };
        }
        return ConversationAgent.Merge(state.Facts, facts) == state.Facts;
    }
    private string[] Select(ConversationState state, CapabilityArguments args, string[] recent)
    {
        if (args.Candidate.HasValue)
        {
            var index = args.Candidate.Value - 1;
            if (index >= state.Candidates.Length) throw new InvalidOperationException();
            return [state.Candidates[index]];
        }
        var requested = args.Vehicles ?? (recent.Length > 0 ? recent : state.Alternative is null ? [] : [state.Alternative]);
        if (args.IncludeRecent == true) requested = [.. recent, .. requested];
        return requested.Select(id =>
        {
            if (id == "waiver" || data.Catalog.ContainsKey(id)) return id;
            var matches = tools.LookupVehicle(new(id));
            return matches.Length == 1 ? matches[0].Id : id;
        }).Distinct().ToArray();
    }
    private string Label(string selection) => selection == "waiver" ? "ויתור על רכב" : data.Catalog.TryGetValue(selection, out var vehicle) ? vehicle.CommercialName : "טרם נבחר רכב מהקטלוג";
    private string Policy(string topic)
    {
        string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("he-IL")) + " ₪";
        var policy = data.Rules.Policy;
        return topic switch
        {
            "groups" => "תקציבי הרכב מהמקור: " + string.Join(" · ", policy.GroupOrder.Select(group => $"{group}: {Money(policy.Budgets[group])}")) + ". בחירת רכב רגיל היא בקבוצה המאומתת או נמוכה יותר ובמסגרת התקציב. שאלה היפותטית אינה משנה זכאות; יתרת תקציב אינה משולמת בשכר.",
            "electric" => $"לרכב חשמלי זכאי, המנוע מגלם החזר חשמל לתוספת נטו של {Money(data.Rules.Business.ElectricReimbursementNetTarget)}. שווי שימוש והשתתפות עדיין נכללים בחישוב הפיקטיבי.",
            "family" => $"מסלול משפחתי דורש לפחות {policy.Family.MinChildren} ילדים עד גיל {policy.Family.ChildMaxAge}. העלות וחלוקת הפער מחושבות לכל רכב לפי המקור.",
            "waiver" => "ויתור מותר רק לעיסוקים שהכללים מאפשרים להם זאת. אחזקת רכב היא הכנסה חייבת ולא פנסיונית; המנוע מחשב את השינוי בנטו.",
            _ => $"החישוב משתמש בלוחות שנת {data.Rules.PayrollTaxYear} שבמקור: מס, ביטוח, זיכויים והפרשות. עיגול לשקל מתבצע בתצוגה בלבד; אין שינוי בכללי השכר בעקבות הודעה בשיחה."
        };
    }
}
