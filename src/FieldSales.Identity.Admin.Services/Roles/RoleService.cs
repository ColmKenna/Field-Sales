using FieldSales.Identity.Services.Users;
using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Validation;

namespace FieldSales.Identity.Services.Roles;

public class RoleService(
    IRoleAdministrationStore store,
    IAuditWriter auditWriter) : IRoleService
{
    // We pass SysAdminRole as the protected role that cannot be deleted.
    private const string ProtectedRoleName = ProtectedAdminRoles.SysAdmin;
    private readonly CategoryAudit audit = AuditOperation.For(auditWriter, AuditCategory.Role);
    private readonly IRoleAdministrationStore store = store;

    public Task<ListResult<RoleListItem>> GetRolesAsync(
        ListQuery query,
        CancellationToken cancellationToken = default) =>
        store.GetRolesAsync(query, cancellationToken);

    public Task<RoleDetailsModel?> GetRoleAsync(RoleId roleId, CancellationToken cancellationToken = default) =>
        store.FindRoleAsync(roleId, cancellationToken);

    public async Task<RoleCreateResult> CreateRoleAsync(RoleCreateInputModel input,
        CancellationToken cancellationToken = default)
    {
        return await this.audit.RunAsync<RoleCreateResult>(AuditAction.Create, null, input.Name, async audit =>
        {
            (RoleCreateOutcome Status, RoleId? RoleId, string? ErrorMessage) outcome =
                await store.CreateRoleAsync(input, cancellationToken);
            switch (outcome.Status)
            {
                case RoleCreateOutcome.NameCollision:
                    await this.audit.DeniedAsync(AuditAction.Create, AuditReasonCode.NameCollision, null, input.Name,
                        "A role with that name already exists.", cancellationToken);
                    return RoleCreateResult.Failed("A role with that name already exists.");

                case RoleCreateOutcome.ValidationFailed:
                    await this.audit.DeniedAsync(AuditAction.Create, AuditReasonCode.ValidationFailed, null, input.Name,
                        outcome.ErrorMessage ?? "Validation failed.", cancellationToken);
                    return RoleCreateResult.Failed(outcome.ErrorMessage ?? "Validation failed.");
            }

            await this.audit.SucceededAsync(AuditAction.Create, outcome.RoleId, input.Name,
                "Role created", cancellationToken);

            return RoleCreateResult.Succeeded(outcome.RoleId!.Value);
        }, cancellationToken);
    }

    public async Task<AdminMutationResult> DeleteRoleAsync(RoleId roleId, CancellationToken cancellationToken = default)
    {
        return await this.audit.RunAsync<AdminMutationResult>(AuditAction.Delete, roleId.Value, roleId.Value, async audit =>
        {
            (RoleDeleteOutcome Status, string TargetName) outcome =
                await store.DeleteRoleAsync(roleId, ProtectedRoleName, cancellationToken);
            audit.TargetName = outcome.TargetName;
            switch (outcome.Status)
            {
                case RoleDeleteOutcome.RoleNotFound:
                    await this.audit.DeniedAsync(AuditAction.Delete, AuditReasonCode.NotFound, roleId, outcome.TargetName,
                        "Role not found.", cancellationToken);
                    return new AdminMutationResult(AdminMutationStatus.NotFound,
                        new ValidationErrorDictionary().AddError(string.Empty, "Role not found."));

                case RoleDeleteOutcome.ProtectedRoleBlocked:
                    await this.audit.DeniedAsync(AuditAction.Delete, AuditReasonCode.ProtectedResource, roleId,
                        outcome.TargetName,
                        "Cannot delete protected role.", cancellationToken);
                    return AdminMutationResult.DeniedResult(string.Empty, "You cannot delete this protected role.");

                case RoleDeleteOutcome.ValidationFailed:
                    await this.audit.DeniedAsync(AuditAction.Delete, AuditReasonCode.ValidationFailed, roleId,
                        outcome.TargetName,
                        "Failed to delete role.", cancellationToken);
                    return AdminMutationResult.ValidationFailure(string.Empty, "Failed to delete role.");
            }

            await this.audit.SucceededAsync(AuditAction.Delete, roleId, outcome.TargetName,
                "Role deleted manually by administrator", cancellationToken);

            return AdminMutationResult.Success();
        }, cancellationToken);
    }
}
