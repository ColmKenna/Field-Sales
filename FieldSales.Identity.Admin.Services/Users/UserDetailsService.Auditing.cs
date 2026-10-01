using FieldSales.Identity.Services.AuditLogs;

namespace FieldSales.Identity.Services.Users;

public partial class UserDetailsService
{
    private Task AuditDeniedAsync(AuditAction action, AuditReasonCode reasonCode, string targetId, string targetName,
        string details, CancellationToken cancellationToken)
        => _auditWriter.WriteAsync(new AdminAuditEvent(
            AuditCategory.User, action, AuditOutcome.Denied, reasonCode,
            targetId, targetName, Details: details), cancellationToken);

    private Task<T> ExecuteAuditedAsync<T>(
        AuditAction action,
        string userId,
        Func<AuditOperation, Task<T>> operation,
        CancellationToken cancellationToken) =>
        AuditOperation.RunAsync(_auditWriter, AuditCategory.User, action, userId, userId, operation, cancellationToken);

    private Task AuditFailedAsync(AuditAction action, string targetId, string targetName, Exception ex,
        CancellationToken cancellationToken) => AuditOperation.WriteFailureAsync(
            _auditWriter, AuditCategory.User, action, targetId, targetName, ex, cancellationToken);
}