using FieldSales.Identity.Services.Persistence;
using FieldSales.Identity.Services.Validation;
using Duende.IdentityServer.Models;
using FieldSales.Identity.Services.AuditLogs;
using Microsoft.EntityFrameworkCore;
using PersistedGrant = Duende.IdentityServer.EntityFramework.Entities.PersistedGrant;

namespace FieldSales.Identity.Services.Users;

public partial class UserDetailsService
{
    public Task<UserAccessRevokeResult> RevokeUserAccessAsync(UserActionContext context,
        CancellationToken cancellationToken = default) =>
        ExecuteAuditedAsync(AuditAction.RevokeUserAccess, context.Target.Value, audit => RevokeUserAccessCoreAsync(context, audit, cancellationToken), cancellationToken);

    private async Task<UserAccessRevokeResult> RevokeUserAccessCoreAsync(UserActionContext context,
        AuditOperation audit, CancellationToken cancellationToken)
    {
        UserId userId = context.Target;
        if (string.IsNullOrWhiteSpace(userId))
            return await DenyRevokeAccessUserNotFoundAsync(userId, cancellationToken);

        string targetName = userId.Value;

        audit.TargetName = targetName;

        SecurityStampRotationOutcome rotation = await _store.RotateSecurityStampAsync(context, cancellationToken);
        targetName = rotation.TargetName;
        audit.TargetName = targetName;

        if (rotation.Status == SecurityStampRotationStatus.UserNotFound)
            return await DenyRevokeAccessUserNotFoundAsync(userId, cancellationToken);

        if (rotation.Status == SecurityStampRotationStatus.SelfActionBlocked)
            return await DenyRevokeAccessSelfActionAsync(userId, targetName, cancellationToken);

        var (clientIds, revokedCount) = await IdentityTransactions.RunAsync(_persistedGrantDbContext, null, async transaction =>
        {
            var clientIds = await _persistedGrantDbContext.PersistedGrants
                .Where(g => g.SubjectId == userId && g.ClientId != null && g.ClientId != "")
                .Select(g => g.ClientId!)
                .Distinct()
                .ToListAsync(cancellationToken);

            int revokedCount = await _persistedGrantDbContext.PersistedGrants
                .Where(g => g.SubjectId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return (clientIds, revokedCount);
        }, cancellationToken);

        await _auditWriter.WriteAsync(new AdminAuditEvent(
                AuditCategory.User, AuditAction.RevokeUserAccess, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
                userId, targetName,
                Details: $"Rotated the security stamp and revoked {revokedCount} persisted grant(s)"),
            cancellationToken);

        // External I/O is deliberately outside both database transactions. A failed client
        // notification cannot roll back the committed security-stamp rotation or grant deletion.
        string? warningMessage = null;
        try
        {
            await _backChannelLogoutService.SendLogoutNotificationsAsync(new LogoutNotificationContext
            {
                SubjectId = userId,
                ClientIds = clientIds
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await _auditWriter.WriteAsync(new AdminAuditEvent(
                AuditCategory.User, AuditAction.SendBackChannelLogout, AuditOutcome.Failed,
                AuditReasonCode.NotificationFailure, userId, targetName,
                Details: $"Back-channel logout notification failed ({ex.GetType().Name})"), cancellationToken);
            warningMessage = "Access was revoked locally, but one or more clients could not be notified.";
        }

        return UserAccessRevokeResult.Succeeded(revokedCount, warningMessage);
    }

    private async Task<UserAccessRevokeResult> DenyRevokeAccessUserNotFoundAsync(UserId userId, CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.RevokeUserAccess, AuditReasonCode.NotFound, userId, userId,
            "User not found.", cancellationToken);
        return UserAccessRevokeResult.Failed("User not found.", AdminMutationStatus.NotFound);
    }

    private async Task<UserAccessRevokeResult> DenyRevokeAccessSelfActionAsync(UserId userId, string targetName, CancellationToken cancellationToken)
    {
        string message =
            "You cannot revoke your own access from this page. Ask another administrator to do this if needed.";
        await AuditDeniedAsync(AuditAction.RevokeUserAccess, AuditReasonCode.SelfAction, userId, targetName,
            message, cancellationToken);
        return UserAccessRevokeResult.Failed(message);
    }
}