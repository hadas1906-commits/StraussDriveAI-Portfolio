using System.Text.Json;
using System.Text.Json.Serialization;

namespace StraussDrive.Api.Advisor;

public sealed record AdvisorCommand(string Name, JsonElement Arguments);
public sealed record CapabilityArguments(string[]? Vehicles = null, int? Candidate = null, string? Drive = null,
    string? Topic = null, EmployeeFacts? Facts = null, string? Category = null, string? Group = null, string? Sort = null, bool? Recommend = null, bool? TableOnly = null, bool? IncludeRecent = null);

/// <summary>The same allowlist supplies provider schemas and authoritative server validation.</summary>
public static class AdvisorCapabilities
{
    private static readonly Dictionary<string, string[]> Fields = new()
    {
        ["get_active_employee"] = [], ["get_eligible_vehicles"] = ["drive", "sort"],
        ["retrieve_policy"] = ["topic"], ["calculate_vehicle_comparison"] = ["vehicles", "candidate", "recommend", "tableOnly", "includeRecent"],
        ["validate_eligibility"] = ["vehicles", "candidate"], ["explain_calculation"] = [],
        ["propose_fact_changes"] = ["facts"], ["create_representative_request"] = ["category"],
        ["cancel_representative_request"] = [], ["record_group_claim"] = ["group"], ["discard_proposals"] = [], ["unsupported_question"] = []
    };
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        ["get_active_employee"] = "Use the current authorized employee context; never choose an employee identity.",
        ["get_eligible_vehicles"] = "List fictional eligible vehicles and update preference: all, electric or family. Sort by catalog or monthly_cost ascending; candidate refers to this displayed ordering.",
        ["retrieve_policy"] = "Read fictional policy: electric, family, waiver, tax or groups/budgets.",
        ["calculate_vehicle_comparison"] = "Compare up to three explicit catalog IDs or waiver against the current vehicle. Use candidate (1-based) for an ordinal follow-up. Omit selection to recalculate recent comparisons, falling back to the last alternative when no recent list exists. Set recommend for a financial ranking/difference question, tableOnly when only a table is requested, includeRecent when adding a vehicle to the previous comparison. If no alternative exists the app asks for one.",
        ["validate_eligibility"] = "Check selected vehicles using fictional entitlement. Never decide eligibility yourself.",
        ["explain_calculation"] = "Explain current salary with the deterministic calculation engine.",
        ["propose_fact_changes"] = "Propose only newly stated employee facts/corrections. The employee must confirm before use; never infer occupation from a group or alter rules.",
        ["create_representative_request"] = "Propose human review for an explicit employee request; category Participation or BusinessQuestion. Server requires separate employee confirmation before persistence.",
        ["discard_proposals"] = "Discard unconfirmed changes or a handoff proposal when the employee rejects/corrects them or changes topic. Never cancels a persisted ticket.",
        ["record_group_claim"] = "Record an explicitly stated entitlement group as an unverified claim and resolve any conflict against the server's verified profile; never changes entitlement.",
        ["cancel_representative_request"] = "Propose closing the current employee's pending ticket, for cancellation or changing vehicles. Requires separate employee confirmation. Only function in this turn; compare a new selection after confirmation.",
        ["unsupported_question"] = "Question is outside company-car benefits, or cannot safely be interpreted. No side effects."
    };
    public static object[] Definitions => Fields.Select(entry => new
    {
        type = "function", function = new { name = entry.Key, description = Descriptions[entry.Key], parameters = new
        {
            type = "object", additionalProperties = false,
            properties = entry.Value.ToDictionary(argument => argument, argument => Schema(argument))
        } }
    }).Cast<object>().ToArray();
    // One function carries the requested outcome separately from a bounded allowlisted plan.
    // Existing native calls remain readable for stored/test/legacy plans.
    public static object[] PlanningDefinitions => [new {
        type = "function", function = new {
            name = "plan_advisor_turn",
            description = "Describe the employee's requested outcome independently of the steps. Comparison targets are new alternatives; the current baseline is implicit. Use the reserved target current only for a request about the unchanged current arrangement itself, and waiver for no company car. A financial request for explicit alternatives (including waiver) is comparison even if you first list options, check eligibility or explain. Discovery is only for finding options without a requested calculation. Never provide identities, money or entitlement.",
            parameters = new {
                type = "object", additionalProperties = false, required = new[] { "goal", "commands" },
                properties = new {
                    goal = new { type = "object", additionalProperties = false, required = new[] { "kind" }, properties = new {
                        kind = new { type = "string", @enum = new[] { "comparison", "discovery", "explanation", "policy", "other" } },
                        comparison = Parameters(Fields["calculate_vehicle_comparison"])
                    } },
                    commands = new { type = "array", minItems = 1, maxItems = 6, items = new {
                        anyOf = Fields.Select(entry => new { type = "object", additionalProperties = false,
                            required = new[] { "name", "arguments" }, properties = new {
                                name = new { type = "string", @enum = new[] { entry.Key } }, arguments = Parameters(entry.Value)
                            } }).ToArray()
                    } }
                }
            }
        }
    }];
    private static object Parameters(string[] fields) => new { type = "object", additionalProperties = false,
        properties = fields.ToDictionary(field => field, Schema) };
    public static TurnPlan ParseTurn(JsonElement calls)
    {
        // Parse legacy calls through the same strict validator; do not invent a missing goal.
        if (calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() is < 1 or > 6) throw new InvalidOperationException("Invalid capability count.");
        var wrappers = calls.EnumerateArray().Count(call => call.GetProperty("function").GetProperty("name").GetString() == "plan_advisor_turn");
        if (wrappers == 0) return new("tools") { Commands = Parse(calls) };
        if (wrappers != 1 || calls.GetArrayLength() != 1) throw new InvalidOperationException("Conflicting plans.");
        RejectDuplicates(calls);
        var call = calls[0];
        if (call.GetProperty("type").GetString() != "function") throw new InvalidOperationException("Invalid capability type.");
        using var json = JsonDocument.Parse(call.GetProperty("function").GetProperty("arguments").GetString()!);
        var root = json.RootElement;
        Only(root, "goal", "commands"); RejectDuplicates(root);
        var request = root.GetProperty("goal"); Only(request, "kind", "comparison");
        var kind = request.GetProperty("kind").GetString()!;
        AdvisorCommand? comparison = request.TryGetProperty("comparison", out var selection) && selection.ValueKind != JsonValueKind.Null
            ? new("calculate_vehicle_comparison", selection.Clone()) : null;
        var goal = new AdvisorGoal(kind, comparison); AdvisorGoals.Completion(goal);
        var steps = root.GetProperty("commands");
        if (steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() is < 1 or > 6) throw new InvalidOperationException("Invalid plan count.");
        var commands = steps.EnumerateArray().Select(step => {
            Only(step, "name", "arguments");
            var command = new AdvisorCommand(step.GetProperty("name").GetString()!, step.GetProperty("arguments").Clone());
            Validate(command); return command;
        }).ToArray();
        return new("tools") { Commands = commands, Goal = goal };
    }
    private static void Only(JsonElement element, params string[] fields)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Any(p => !fields.Contains(p.Name)))
            throw new InvalidOperationException("Invalid plan field.");
    }
    private static object Schema(string field) => field switch
    {
        "vehicles" => new { type = "array", maxItems = 3, minItems = 1, items = new { type = "string", maxLength = 40 } },
        "recommend" or "tableOnly" or "includeRecent" => new { type = "boolean" },
        "candidate" => new { type = "integer", minimum = 1, maximum = 20 },
        "drive" => new { type = "string", @enum = new[] { "all", "electric", "family" } },
        "topic" => new { type = "string", @enum = new[] { "electric", "family", "waiver", "tax", "groups" } },
        "category" => new { type = "string", @enum = new[] { "Participation", "BusinessQuestion" } },
        "group" => new { type = "string", @enum = new[] { "A", "B", "C", "D", "א", "ב", "ג", "ד" } },
        "sort" => new { type = "string", @enum = new[] { "catalog", "monthly_cost" } },
        _ => new { type = "object", additionalProperties = false, properties = new Dictionary<string, object>
        {
            ["occupation"] = new { type = "string", maxLength = 200 }, ["currentCar"] = new { type = "string", maxLength = 40 },
            ["baseSalary"] = new { type = "number", minimum = 0, maximum = 100000000 },
            ["commission"] = new { type = "number", minimum = 0, maximum = 100000000 },
            ["bonus"] = new { type = "number", minimum = 0, maximum = 100000000 },
            ["pensionPercent"] = new { type = "number", minimum = 0, maximum = 100 },
            ["studyFundPercent"] = new { type = "number", minimum = 0, maximum = 100 },
            ["creditPoints"] = new { type = "number", minimum = 0, maximum = 100 },
            ["eligibleChildren"] = new { type = "integer", minimum = 0, maximum = 100 },
            ["currentParticipation"] = new { type = "number", minimum = 0, maximum = 1000000 }
        } }
    };
    public static CapabilityArguments Validate(AdvisorCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || !Fields.TryGetValue(command.Name, out var fields) || command.Arguments.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Invalid capability.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in command.Arguments.EnumerateObject())
            if (!seen.Add(field.Name) || !fields.Contains(field.Name)) throw new InvalidOperationException("Invalid capability argument.");
        RejectDuplicates(command.Arguments);
        var args = command.Arguments.Deserialize<CapabilityArguments>(new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })!;
        if (args.Vehicles is { Length: < 1 or > 3 } || args.Vehicles?.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 40) == true
            || args.Candidate is < 1 or > 20 || args.Candidate.HasValue && args.Vehicles is not null
            || args.Drive is not (null or "all" or "electric" or "family")
            || args.Topic is not (null or "electric" or "family" or "waiver" or "tax" or "groups")
            || args.Category is not (null or "Participation" or "BusinessQuestion")
            || args.Sort is not (null or "catalog" or "monthly_cost")
            || args.Group is not (null or "A" or "B" or "C" or "D" or "א" or "ב" or "ג" or "ד")) throw new InvalidOperationException("Invalid capability value.");
        if (command.Name == "record_group_claim" && args.Group is null) throw new InvalidOperationException("Missing group claim.");
        if (command.Name == "propose_fact_changes")
        {
            if (args.Facts is null) throw new InvalidOperationException("Missing proposed facts.");
            AdvisorTools.Validate(args.Facts);
            if (args.Facts.Occupation is { Length: > 200 } || args.Facts.CurrentCar is { Length: > 40 }) throw new InvalidOperationException("Invalid fact value.");
        }
        return args;
    }
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array) { foreach (var item in value.EnumerateArray()) RejectDuplicates(item); return; }
        if (value.ValueKind != JsonValueKind.Object) return;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        { if (!seen.Add(property.Name)) throw new InvalidOperationException("Duplicate capability argument."); RejectDuplicates(property.Value); }
    }
    public static AdvisorCommand[] Parse(JsonElement calls)
    {
        if (calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() is < 1 or > 6) throw new InvalidOperationException("Invalid capability count.");
        var commands = calls.EnumerateArray().Select(call =>
        {
            RejectDuplicates(call);
            if (call.GetProperty("type").GetString() != "function") throw new InvalidOperationException("Invalid capability type.");
            var function = call.GetProperty("function");
            using var json = JsonDocument.Parse(function.GetProperty("arguments").GetString()!);
            return new AdvisorCommand(function.GetProperty("name").GetString()!, json.RootElement.Clone());
        }).ToArray();
        try { foreach (var command in commands) Validate(command); }
        catch (AdvisorException) { throw new InvalidOperationException("Invalid capability facts."); }
        return commands;
    }
}
