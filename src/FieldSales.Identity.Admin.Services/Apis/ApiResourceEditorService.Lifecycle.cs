using Duende.IdentityServer.EntityFramework.Entities;
using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Scopes;
using FieldSales.Identity.Services.Validation;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Identity.Services.Apis;

public partial class ApiResourceEditorService
{
    public Task<AdminMutationResult> SetEnabledAsync(ScopeName name, bool enabled,
        CancellationToken cancellationToken = default) =>
        _audit.RunAsync(
            AuditAction.SetEnabled,
            name.Value,
            name.Value,
            audit => SetEnabledCoreAsync(name.Value, enabled, audit, cancellationToken),
            cancellationToken);

    private async Task<AdminMutationResult> SetEnabledCoreAsync(string name, bool enabled,
        AuditOperation audit, CancellationToken cancellationToken = default)
    {
        name = name?.Trim() ?? string.Empty;
        ApiResource? entity =
            await _configurationDbContext.ApiResources.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
        if (entity is null)
            return await DenyResourceNotFoundAsync(AuditAction.SetEnabled, name, cancellationToken);

        audit.TargetName = name;

        entity.Enabled = enabled;
        await _configurationDbContext.SaveChangesAsync(cancellationToken);

        await _audit.SucceededAsync(AuditAction.SetEnabled, name, name,
            $"Set API Resource enabled status to {enabled}", cancellationToken);

        return AdminMutationResult.Success();
    }

    public Task<AdminMutationResult> DeleteAsync(ScopeName name, CancellationToken cancellationToken = default) =>
        _audit.RunAsync(
            AuditAction.Delete,
            name.Value,
            name.Value,
            audit => DeleteCoreAsync(name.Value, audit, cancellationToken),
            cancellationToken);

    private async Task<AdminMutationResult> DeleteCoreAsync(string name, AuditOperation audit, CancellationToken cancellationToken = default)
    {
        name = name?.Trim() ?? string.Empty;
        ApiResource? entity =
            await _configurationDbContext.ApiResources.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
        if (entity is null)
            return await DenyResourceNotFoundAsync(AuditAction.Delete, name, cancellationToken);

        audit.TargetName = name;

        _configurationDbContext.ApiResources.Remove(entity);
        await _configurationDbContext.SaveChangesAsync(cancellationToken);

        await _audit.SucceededAsync(AuditAction.Delete, name, name,
            $"Deleted API Resource '{name}'", cancellationToken);

        return AdminMutationResult.Success();
    }
}
