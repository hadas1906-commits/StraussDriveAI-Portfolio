using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

namespace StraussDrive.Api.Advisor;

// Only allowlisted categories and locally generated metadata leave this boundary.
public sealed record ProviderDiagnostic(string Outcome, int? HttpStatus, long LatencyMs, string AttemptId,
    int? InputTokens = null, int? OutputTokens = null);
public sealed record ProviderResult(TurnPlan Plan, ProviderDiagnostic Diagnostic);
public interface IDiagnosedConversationProvider : IConversationProvider
{
    Task<ProviderResult> PlanWithDiagnosticsAsync(string text, ConversationState state, CancellationToken cancellation);
}
public sealed class ProviderFailureException(ProviderDiagnostic diagnostic) : InvalidOperationException("AI provider failed: " + diagnostic.Outcome)
{
    public ProviderDiagnostic Diagnostic { get; } = diagnostic;
}
public static class ProviderFailures
{
    public static string Classify(Exception exception, CancellationToken caller)
    {
        if (exception is OperationCanceledException) return caller.IsCancellationRequested ? "cancelled" : "timeout";
        if (exception is HttpRequestException http && http.StatusCode is { } status) return status switch
        {
            HttpStatusCode.Unauthorized => "authentication_failed",
            HttpStatusCode.Forbidden => "permission_denied",
            HttpStatusCode.NotFound => "model_or_endpoint_unavailable",
            HttpStatusCode.TooManyRequests => "rate_or_quota_limited",
            HttpStatusCode.BadRequest => "request_rejected",
            _ => "http_error"
        };
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is AuthenticationException) return "tls_error";
        if (exception is HttpRequestException httpError)
            return httpError.HttpRequestError == HttpRequestError.SecureConnectionError ? "tls_error" : "network_error";
        if (exception is IOException or SocketException) return "network_error";
        if (exception is System.Text.Json.JsonException) return "invalid_json_or_schema";
        if (exception is FormatException or ArgumentException) return "configuration_error";
        return "invalid_response_or_configuration";
    }
}
