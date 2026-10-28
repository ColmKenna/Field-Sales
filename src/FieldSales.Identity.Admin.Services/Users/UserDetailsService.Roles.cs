using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Validation;

namespace FieldSales.Identity.Services.Users;

public partial class UserDetailsService
{
    public Task<RoleChangeResult> AddRoleAsync(UserId userId, string role,
        CancellationToken cancellationToken = default) =>
        _audit.RunAsync(
            AuditAction.AddRole,
            userId.Value,
            audit => AddRoleCoreAsync(userId, role ?? string.Empty, audit, cancellationToken),
            cancellationToken);

    private async Task<RoleChangeResult> AddRoleCoreAsync(UserId userId, string role,
        AuditOperation audit, CancellationToken cancellationToken)
    {
        RoleAdditionOutcome outcome = await _store.AddRoleAsync(userId, role, cancellationToken);
        audit.TargetName = outcome.TargetName;

        switch (outcome.Status)
        {
            case RoleAdditionStatus.UserNotFound:
                await _audit.DeniedAsync(AuditAction.AddRole, AuditReasonCode.NotFound, userId, outcome.TargetName,
                    "User not found.", cancellationToken);
                return RoleChangeResult.Failed("User not found.", AuditReasonCode.NotFound, AdminMutationStatus.NotFound);

            case RoleAdditionStatus.RoleNotFound:
                await _audit.DeniedAsync(AuditAction.AddRole, AuditReasonCode.NotFound, userId, outcome.TargetName,
                    "Role not found.", cancellationToken);
                return RoleChangeResult.Failed("Role not found.", AuditReasonCode.NotFound, AdminMutationStatus.NotFound);

            case RoleAdditionStatus.AlreadyMember:
                return RoleChangeResult.Succeeded();

            case RoleAdditionStatus.ValidationFailed:
                await _audit.DeniedAsync(AuditAction.AddRole, AuditReasonCode.ValidationFailed, userId,
                    outcome.TargetName,
                    outcome.ErrorMessage!, cancellationToken);
                return RoleChangeResult.Failed(outcome.ErrorMessage!);
        }

        await _audit.SucceededAsync(AuditAction.AddRole, userId, outcome.TargetName,
            $"Added role '{role}'", cancellationToken);

        return RoleChangeResult.Succeeded();
    }

    public Task<RoleChangeResult> RemoveRoleAsync(UserId userId, string role,
        CancellationToken cancellationToken = default) => _audit.RunAsync(AuditAction.RemoveRole, userId.Value,
            audit => RemoveRoleCoreAsync(userId, role, audit, cancellationToken), cancellationToken);

    private async Task<RoleChangeResult> RemoveRoleCoreAsync(UserId userId, string role, AuditOperation audit,
        CancellationToken cancellationToken)
    {
        string targetName = userId.Value;
        audit.TargetName = targetName;
        RoleRemovalOutcome outcome;

        audit.TargetName = targetName;

        var actorId = UserId.Create(AuditActorResolver.Resolve(_httpContextAccessor.HttpContext?.User).SubjectId);
        outcome = await _store.RemoveRoleAsync(userId, role, ProtectedAdminRoles.SysAdmin, actorId,
            cancellationToken);

        targetName = outcome.TargetName;

        audit.TargetName = targetName;

        switch (outcome.Status)
        {
            case RoleRemovalStatus.UserNotFound:
                await _audit.DeniedAsync(AuditAction.RemoveRole, AuditReasonCode.NotFound, userId.Value, targetName,
                    "User not found.", cancellationToken);
                return RoleChangeResult.Failed("User not found.", AuditReasonCode.NotFound,
                    AdminMutationStatus.NotFound);

            case RoleRemovalStatus.RoleNotFound:
                await _audit.DeniedAsync(AuditAction.RemoveRole, AuditReasonCode.NotFound, userId.Value, targetName,
                    "Role not found.", cancellationToken);
                return RoleChangeResult.Failed("Role not found.", AuditReasonCode.NotFound,
                    AdminMutationStatus.NotFound);

            case RoleRemovalStatus.SelfDemotionBlocked:
                {
                    string message =
                        "You cannot remove your own SysAdmin role. Ask another administrator to do this if needed.";
                    await _audit.DeniedAsync(AuditAction.RemoveRole, AuditReasonCode.SelfDemotion, userId.Value, targetName,
                        message, cancellationToken);
                    return RoleChangeResult.Failed(message, AuditReasonCode.SelfDemotion);
                }

            case RoleRemovalStatus.LastProtectedMemberBlocked:
                {
                    string message =
                        $"'{targetName}' is the last SysAdmin. Assign the role to another user before removing it here.";
                    await _audit.DeniedAsync(AuditAction.RemoveRole, AuditReasonCode.LastAdministrator, userId.Value,
                        targetName,
                        message, cancellationToken);
                    return RoleChangeResult.Failed(message, AuditReasonCode.LastAdministrator);
                }

            case RoleRemovalStatus.ValidationFailed:
                await _audit.DeniedAsync(AuditAction.RemoveRole, AuditReasonCode.ValidationFailed, userId.Value,
                    targetName,
                    outcome.ErrorMessage!, cancellationToken);
                return RoleChangeResult.Failed(outcome.ErrorMessage!, AuditReasonCode.ValidationFailed,
                    AdminMutationStatus.ValidationFailed);
        }

        await _audit.SucceededAsync(AuditAction.RemoveRole, userId.Value, targetName,
            outcome.RoleWasRemoved ? $"Removed role '{role}'" : $"Role '{role}' was not assigned", cancellationToken);

        if (outcome.RoleWasRemoved)
            // This runs after the role transaction commits. Its own durable mutations complete
            // before any back-channel call is made.
            await RevokeUserAccessAsync(new UserActionContext(userId, null), cancellationToken);

        return RoleChangeResult.Succeeded();
    }
}
