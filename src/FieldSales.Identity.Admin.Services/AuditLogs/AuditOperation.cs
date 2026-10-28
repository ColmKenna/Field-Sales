using System.Diagnostics;

namespace FieldSales.Identity.Services.AuditLogs;

/// <summary>One failure boundary per invocation, with metadata updated as the target is loaded.</summary>
public sealed class AuditOperation
{
    private AuditOperation(string? targetId, string? targetName) => (TargetId, TargetName) = (targetId, targetName);
    public string? TargetId { get; set; }
    public string? TargetName { get; set; }

    public static CategoryAudit For(IAuditWriter writer, AuditCategory category) => new(writer, category);

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

    public static Task WriteDeniedAsync(IAuditWriter writer, AuditCategory category, AuditAction action,
        AuditReasonCode reasonCode, string? targetId, string? targetName, string details, CancellationToken cancellationToken) =>
        writer.WriteAsync(new AdminAuditEvent(category, action, AuditOutcome.Denied, reasonCode,
            targetId, targetName, Details: details), cancellationToken);

    public static Task WriteSucceededAsync(IAuditWriter writer, AuditCategory category, AuditAction action,
        string? targetId, string? targetName, string details, CancellationToken cancellationToken,
        object? oldValue = null, object? newValue = null) =>
        writer.WriteAsync(new AdminAuditEvent(category, action, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
            targetId, targetName, oldValue, newValue, details), cancellationToken);

    public static Task WriteFailureAsync(IAuditWriter writer, AuditCategory category, AuditAction action,
        string? targetId, string? targetName, Exception exception, CancellationToken cancellationToken) =>
        writer.WriteAsync(new AdminAuditEvent(category, action, AuditOutcome.Failed, AuditReasonCode.PersistenceFailure,
            targetId, targetName, Details: $"Unexpected error ({exception.GetType().Name})"), cancellationToken);
}

public readonly record struct CategoryAudit(IAuditWriter Writer, AuditCategory Category)
{
    public Task<T> RunAsync<T>(AuditAction action, string? targetId, string? targetName,
        Func<AuditOperation, Task<T>> operation, CancellationToken cancellationToken) =>
        AuditOperation.RunAsync(Writer, Category, action, targetId, targetName, operation, cancellationToken);

    public Task<T> RunAsync<T>(AuditAction action, string? targetId,
        Func<AuditOperation, Task<T>> operation, CancellationToken cancellationToken) =>
        AuditOperation.RunAsync(Writer, Category, action, targetId, targetId, operation, cancellationToken);

    public Task DeniedAsync(AuditAction action, AuditReasonCode reasonCode, string? targetId, string? targetName,
        string details, CancellationToken cancellationToken) =>
        AuditOperation.WriteDeniedAsync(Writer, Category, action, reasonCode, targetId, targetName, details, cancellationToken);

    public Task SucceededAsync(AuditAction action, string? targetId, string? targetName, string details,
        CancellationToken cancellationToken, object? oldValue = null, object? newValue = null) =>
        AuditOperation.WriteSucceededAsync(Writer, Category, action, targetId, targetName, details, cancellationToken, oldValue, newValue);

    public Task FailedAsync(AuditAction action, string? targetId, string? targetName, Exception exception,
        CancellationToken cancellationToken) =>
        AuditOperation.WriteFailureAsync(Writer, Category, action, targetId, targetName, exception, cancellationToken);
}
