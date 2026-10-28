using FieldSales.Identity.Services.Validation;
using FieldSales.Identity.Services.AuditLogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PersistedGrant = Duende.IdentityServer.EntityFramework.Entities.PersistedGrant;

namespace FieldSales.Identity.Services.Users;

public partial class UserDetailsService
{
    public Task<PasswordResetResult> ResetPasswordAsync(UserId userId, string newPassword,
        CancellationToken cancellationToken = default) =>
        _audit.RunAsync(
            AuditAction.ResetPassword,
            userId.Value,
            audit => ResetPasswordCoreAsync(userId, newPassword, audit, cancellationToken),
            cancellationToken);

    private async Task<PasswordResetResult> ResetPasswordCoreAsync(UserId userId, string newPassword,
        AuditOperation audit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return await DenyPasswordRequiredAsync(userId, cancellationToken);

        PasswordResetOutcome outcome = await _store.ResetPasswordAsync(userId, newPassword, cancellationToken);
        audit.TargetName = outcome.TargetName;
        switch (outcome.Status)
        {
            case PasswordResetStatus.UserNotFound:
                await _audit.DeniedAsync(AuditAction.ResetPassword, AuditReasonCode.NotFound, userId, outcome.TargetName,
                    "User not found.", cancellationToken);
                return PasswordResetResult.Failed("User not found.", AdminMutationStatus.NotFound);

            case PasswordResetStatus.ValidationFailed:
                await _audit.DeniedAsync(AuditAction.ResetPassword, AuditReasonCode.ValidationFailed, userId,
                    outcome.TargetName,
                    outcome.ErrorMessage!, cancellationToken);
                return PasswordResetResult.Failed(outcome.ErrorMessage!);
        }

        await _audit.SucceededAsync(AuditAction.ResetPassword, userId, outcome.TargetName,
            "Password was reset by administrator", cancellationToken);

        return PasswordResetResult.Succeeded();
    }

    private async Task<PasswordResetResult> DenyPasswordRequiredAsync(UserId userId, CancellationToken cancellationToken)
    {
        await _audit.DeniedAsync(AuditAction.ResetPassword, AuditReasonCode.ValidationFailed, userId, userId,
            "Password is required.", cancellationToken);
        return PasswordResetResult.Failed("Password is required.");
    }

    public Task<UserSuspendResult> SuspendUserAsync(UserActionContext context,
        CancellationToken cancellationToken = default) =>
        _audit.RunAsync(
            AuditAction.SuspendUser,
            context.Target.Value,
            audit => SuspendUserCoreAsync(context, audit, cancellationToken),
            cancellationToken);

    private async Task<UserSuspendResult> SuspendUserCoreAsync(UserActionContext context,
        AuditOperation audit, CancellationToken cancellationToken)
    {
        UserId userId = context.Target;
        UserSuspendOutcome outcome = await _store.SuspendUserAsync(context, cancellationToken);
        audit.TargetName = outcome.TargetName;
        switch (outcome.Status)
        {
            case UserSuspendStatus.UserNotFound:
                await _audit.DeniedAsync(AuditAction.SuspendUser, AuditReasonCode.NotFound, userId, outcome.TargetName,
                    "User not found.", cancellationToken);
                return UserSuspendResult.Failed("User not found.", AdminMutationStatus.NotFound);

            case UserSuspendStatus.SelfActionBlocked:
                await _audit.DeniedAsync(AuditAction.SuspendUser, AuditReasonCode.SelfAction, userId, outcome.TargetName,
                    "Cannot suspend your own account.", cancellationToken);
                return UserSuspendResult.Failed("You cannot suspend your own account.");

            case UserSuspendStatus.ValidationFailed:
                await _audit.DeniedAsync(AuditAction.SuspendUser, AuditReasonCode.ValidationFailed, userId,
                    outcome.TargetName,
                    "Failed to suspend user.", cancellationToken);
                return UserSuspendResult.Failed("Failed to suspend user.");
        }

        await _audit.SucceededAsync(AuditAction.SuspendUser, userId, outcome.TargetName,
            "User suspended manually by administrator", cancellationToken);

        return UserSuspendResult.Succeeded();
    }

    public Task<UserUnlockResult> UnlockUserAsync(UserId userId, CancellationToken cancellationToken = default) =>
        _audit.RunAsync(AuditAction.Unlock, userId.Value, audit => UnlockUserCoreAsync(userId, audit, cancellationToken), cancellationToken);

    private async Task<UserUnlockResult> UnlockUserCoreAsync(UserId userId, AuditOperation audit, CancellationToken cancellationToken)
    {
        UserUnlockOutcome outcome = await _store.UnlockUserAsync(userId, cancellationToken);
        UserUnlockResult result = outcome.Result;
        string targetName = outcome.TargetName;
        audit.TargetName = targetName;

        switch (result.Status)
        {
            case UserUnlockStatus.NotFound:
                await _audit.DeniedAsync(AuditAction.Unlock, AuditReasonCode.NotFound,
                    userId, targetName, "User not found.", cancellationToken);
                return result;

            case UserUnlockStatus.Failed:
                await _audit.DeniedAsync(AuditAction.Unlock, AuditReasonCode.ValidationFailed,
                    userId, targetName, string.Join(" ", result.Errors), cancellationToken);
                return result;
        }

        await _audit.SucceededAsync(AuditAction.Unlock, userId, targetName,
            $"Unlocked user '{targetName}'", cancellationToken);

        return result;
    }

    public Task<UserDeleteResult> DeleteUserAsync(UserActionContext context,
        CancellationToken cancellationToken = default) =>
        _audit.RunAsync(
            AuditAction.DeleteUser,
            context.Target.Value,
            audit => DeleteUserCoreAsync(context, audit, cancellationToken),
            cancellationToken);

    private async Task<UserDeleteResult> DeleteUserCoreAsync(UserActionContext context,
        AuditOperation audit, CancellationToken cancellationToken)
    {
        UserId userId = context.Target;
        UserDeleteOutcome outcome = await _store.DeleteUserAsync(context, cancellationToken);
        audit.TargetName = outcome.TargetName;
        switch (outcome.Status)
        {
            case UserDeleteStatus.UserNotFound:
                await _audit.DeniedAsync(AuditAction.DeleteUser, AuditReasonCode.NotFound, userId, outcome.TargetName,
                    "User not found.", cancellationToken);
                return UserDeleteResult.Failed("User not found.", AdminMutationStatus.NotFound);

            case UserDeleteStatus.SelfActionBlocked:
                await _audit.DeniedAsync(AuditAction.DeleteUser, AuditReasonCode.SelfAction, userId, outcome.TargetName,
                    "Cannot delete your own account.", cancellationToken);
                return UserDeleteResult.Failed("You cannot delete your own account.");

            case UserDeleteStatus.ValidationFailed:
                await _audit.DeniedAsync(AuditAction.DeleteUser, AuditReasonCode.ValidationFailed, userId,
                    outcome.TargetName,
                    "Failed to delete user.", cancellationToken);
                return UserDeleteResult.Failed("Failed to delete user.");
        }

        // Delete grants for the user as well.
        try
        {
            IExecutionStrategy grantStrategy = _persistedGrantDbContext.Database.CreateExecutionStrategy();
            await grantStrategy.ExecuteAsync(async () =>
            {
                _persistedGrantDbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction =
                    await _persistedGrantDbContext.Database.BeginTransactionAsync(cancellationToken);
                List<PersistedGrant> grants = await _persistedGrantDbContext.PersistedGrants
                    .Where(g => g.SubjectId == userId)
                    .ToListAsync(cancellationToken);

                if (grants.Count > 0)
                {
                    _persistedGrantDbContext.PersistedGrants.RemoveRange(grants);
                    await _persistedGrantDbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (Exception ex)
        {
            await _audit.FailedAsync(AuditAction.RemoveGrantsOnUserDelete, userId, outcome.TargetName, ex,
                cancellationToken);
        }

        await _audit.SucceededAsync(AuditAction.DeleteUser, userId, outcome.TargetName,
            "User deleted manually by administrator", cancellationToken);

        return UserDeleteResult.Succeeded();
    }
}
