using System.Text.Json;
using System.Text.Json.Serialization;

namespace StraussDrive.Api.Advisor;

/// <summary>Requested outcome is independent of the model's proposed steps. Contains no authority or money.</summary>
public sealed record AdvisorGoal(string Kind, AdvisorCommand? Comparison = null);

public static class AdvisorGoals
{
    private static readonly string[] ReadSteps = ["get_active_employee", "get_eligible_vehicles", "validate_eligibility", "explain_calculation", "retrieve_policy", "calculate_vehicle_comparison"];
    public static AdvisorCommand? Completion(AdvisorGoal? goal)
    {
        if (goal is null) return null;
        if (goal.Kind is not ("comparison" or "discovery" or "explanation" or "policy" or "other")) throw new InvalidOperationException("Invalid requested outcome.");
        if (goal.Kind != "comparison")
        {
            if (goal.Comparison is not null) throw new InvalidOperationException("Conflicting outcome.");
            return null;
        }
        if (goal.Comparison?.Name != "calculate_vehicle_comparison") throw new InvalidOperationException("Missing comparison outcome.");
        var args = AdvisorCapabilities.Validate(goal.Comparison);
        // Current is an arrangement reference, not a catalog ID or a new choice.
        if (args.Vehicles?.Any(id => id != "current") == true) args = args with { Vehicles = args.Vehicles.Where(id => id != "current").ToArray() };
        else if (args.Vehicles?.Contains("current") == true) args = args with { Vehicles = ["current"] };
        return new("calculate_vehicle_comparison", JsonSerializer.SerializeToElement(args with { Recommend = args.Recommend ?? args.TableOnly != true },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
    }
    public static AdvisorCommand[] Complete(AdvisorCommand[] commands, AdvisorCommand? completion)
    {
        if (completion is null || commands.Any(c => !ReadSteps.Contains(c.Name))) return commands;
        // The outcome owns the targets; stray/duplicate model calculations must not select other cars.
        var steps = commands.Where(c => c.Name != "calculate_vehicle_comparison").DistinctBy(c => (c.Name, c.Arguments.GetRawText())).ToList();
        if (steps.Count == 6)
        {
            // A comparison already supplies the full explanation. Keep the original six-step bound.
            var explicitTargets = AdvisorCapabilities.Validate(completion).Vehicles is { Length: > 0 };
            var redundant = steps.FindIndex(c => c.Name is "explain_calculation" or "get_active_employee" or "validate_eligibility"
                || explicitTargets && c.Name == "get_eligible_vehicles");
            if (redundant < 0) throw new InvalidOperationException("No room for the requested outcome.");
            steps.RemoveAt(redundant);
        }
        steps.Add(completion);
        return steps.ToArray();
    }
}
