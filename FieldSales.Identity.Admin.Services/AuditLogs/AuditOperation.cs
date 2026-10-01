using System.Diagnostics;

namespace FieldSales.Identity.Services.AuditLogs;

/// <summary>One failure boundary per invocation, with metadata updated as the target is loaded.</summary>
public sealed class AuditOperation
{
    private AuditOperation(string? targetId, string? targetName) => (TargetId, TargetName) = (targetId, targetName);
    public string? TargetId { get; set; }
    public string? TargetName { get; set; }

    public static async Task<T> RunAsync<T>(IAuditWriter writer, AuditCategory category, AuditAction action,
        string? targetId, string? targetName, Func<AuditOperation, Task<T>> body, CancellationToken cancellationToken)
    {
        AuditOperation operation = new(targetId, targetName);
        try { return await body(operation); }
        catch (Exception exception)
        {
            try { await WriteFailureAsync(writer, category, action, operation.TargetId, operation.TargetName, exception, cancellationToken); }
            catch (Exception auditFailure)
            {
                // AuditWriter already supplies telemetry fallback. A custom writer must not replace the operation exception.
                Activity.Current?.AddEvent(new ActivityEvent("admin.audit.write-failed", tags: new ActivityTagsCollection
                { ["exception.type"] = auditFailure.GetType().Name }));
            }
            throw;
        }
    }

    public static Task WriteFailureAsync(IAuditWriter writer, AuditCategory category, AuditAction action,
        string? targetId, string? targetName, Exception exception, CancellationToken cancellationToken) =>
        writer.WriteAsync(new AdminAuditEvent(category, action, AuditOutcome.Failed, AuditReasonCode.PersistenceFailure,
            targetId, targetName, Details: $"Unexpected error ({exception.GetType().Name})"), cancellationToken);
}
