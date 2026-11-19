using StraussDrive.Domain;
namespace StraussDrive.Api.Advisor;

public sealed record EmployeeFacts(string? Occupation = null, decimal? BaseSalary = null, decimal? Commission = null,
    decimal? Bonus = null, decimal? PensionPercent = null, decimal? StudyFundPercent = null, decimal? CreditPoints = null,
    int? EligibleChildren = null, string? CurrentCar = null, decimal? CurrentParticipation = null);
public sealed record TurnPlan(string Intent = "compare", string? VehicleQuery = null, EmployeeFacts? Facts = null,
    bool NeedsRepresentative = false, string? QuestionCategory = null, string? GroupQuery = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public AdvisorCommand[]? Commands { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public AdvisorGoal? Goal { get; init; }
}
public sealed record FactEvidence(string Source, string Status);
public sealed record HandoffProposal(string Id, string Category, string? VehicleId, string Target, string Kind = "create", string? TicketId = null);
public sealed class ConversationState
{
    public string[] Candidates { get; set; } = [];
    // Ordered selections from the last successful comparison turn; never cached money.
    public string[] RecentComparisons { get; set; } = [];
    public string Preference { get; set; } = "all";
    public string CandidateSort { get; set; } = "catalog";
    public Dictionary<string, FactEvidence> Evidence { get; set; } = new();
    public Dictionary<string, string> Claims { get; set; } = new();
    public HandoffProposal? ProposedHandoff { get; set; }
    public AdvisorCommand? PendingCapability { get; set; }
    // Set from the conversation's server-owned ProfileKey, never from a message or model plan.
    public string? VerifiedGroup { get; set; }
    public string Stage { get; set; } = "SelectingAlternative";
    public EmployeeFacts Facts { get; set; } = new();
    public string? Alternative { get; set; }
    public string Intent { get; set; } = "compare";
    public string? PendingField { get; set; }
    public string? PendingTicketId { get; set; }
    public decimal? ConfirmedParticipation { get; set; }
    public string? ConfirmedVehicle { get; set; }
    public EmployeeFacts? ProposedFacts { get; set; }
    public TurnPlan? ProposedPlan { get; set; }
}
public sealed record EmployeeSession(string Id, string ProfileKey, string CreatedAt);
public sealed record Conversation(string Id, string OwnerId, string ProfileKey, string Title, ConversationState State, int Version, string CreatedAt, string UpdatedAt);
public sealed record ToolInvocation(string Name, string[] Sources, string Status = "completed");
public sealed record ConversationMessage(string Id, string Role, string Text, string Mode, string CreatedAt,
    ComparisonDto? Comparison, SalaryBreakdown? Breakdown, ToolInvocation[] Tools, SourceLink[] Sources, ProviderDiagnostic? Provider = null,
    ComparisonDto[]? Comparisons = null, ComparisonSummary? Summary = null, ScenarioDto? Baseline = null);
public sealed record SourceLink(string Label, string Url);
public sealed record RepresentativeTicket(string Id, string ConversationId, string Status, string Category, string Question,
    string? VehicleId, string Group, string Occupation, string CreatedAt, string UpdatedAt, int Version, string Target = "alternative");
public sealed record RepresentativeResponse(string Id, string TicketId, string ActorRole, string Action, string Text, decimal? Amount, string CreatedAt, DateOnly? EffectiveFrom = null);
public sealed record AuditEvent(string Id, string TicketId, string ActorId, string ActorRole, string Action, string CreatedAt);
public sealed record EmployeeTicketHistory(RepresentativeTicket Ticket, RepresentativeResponse[] Responses);
public sealed record ConversationView(Conversation Conversation, ConversationMessage[] Messages, RepresentativeTicket? Ticket, RepresentativeResponse[] Responses, EmployeeTicketHistory[]? TicketHistory = null, ConversationAssessment? Assessment = null);
public sealed record TicketView(RepresentativeTicket Ticket, RepresentativeResponse[] Responses, AuditEvent[] Audit);
public sealed record SessionRequest(string? SampleEmployeeId);
public sealed record RepresentativeLoginRequest(string? Code = null);
public sealed record MessageRequest(string Text, int ExpectedVersion, EmployeeFacts? Facts = null, bool ShareWithRepresentative = false, string? ConfirmHandoff = null, bool RequestCancellation = false, string? FactDecision = null);
public sealed record RepresentativeAction(string Action, string Text, int ExpectedVersion, decimal? ParticipationAmount = null, DateOnly? EffectiveFrom = null);
public sealed record SessionView(string Id, string Role, string ProfileKey, bool DemoAuthentication, string AgentMode);
public sealed class AdvisorException(int status, string message) : Exception(message) { public int Status { get; } = status; }
public sealed record AgentOutcome(ConversationMessage Message, RepresentativeTicket? NewTicket = null, string? SupersededTicket = null, string? ClosedTicket = null);
