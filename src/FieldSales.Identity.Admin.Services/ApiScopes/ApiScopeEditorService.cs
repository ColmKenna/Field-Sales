using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Scopes;
using FieldSales.Identity.Services.Validation;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Identity.Services.ApiScopes;

public class ApiScopeEditorService(
    ConfigurationDbContext configurationDbContext,
    IAuditWriter auditWriter) : IApiScopeEditorService
{
    private readonly IAuditWriter _auditWriter = auditWriter;
    private readonly ConfigurationDbContext _configurationDbContext = configurationDbContext;

    public Task<AdminMutationResult> CreateAsync(CreateApiScopeCommand command,
        CancellationToken cancellationToken = default) =>
        CreateAsync(command.Name.Value, command.DisplayName, command.Description, cancellationToken);

    public Task<bool> UpdateBasicsAsync(UpdateApiScopeBasicsCommand command,
        CancellationToken cancellationToken = default) =>
        UpdateBasicsAsync(command.Name.Value, command.DisplayName, command.Description, command.Enabled,
            command.Required,
            command.Emphasize, command.ShowInDiscoveryDocument, cancellationToken);

    public async Task<ApiScopeEditorModel?> GetForEditAsync(ScopeName name,
        CancellationToken cancellationToken = default)
    {
        ApiScope? entity = await LoadScopeAsync(name.Value, true, cancellationToken);
        return entity is null ? null : MapToEditorModel(entity);
    }

    public Task<AdminMutationResult> CreateAsync(
        string name,
        string? displayName,
        string? description,
        CancellationToken cancellationToken = default) =>
        ExecuteAuditedAsync(
            AuditAction.Create,
            name ?? string.Empty,
            displayName ?? name ?? string.Empty,
            audit => CreateCoreAsync(name ?? string.Empty, displayName, description, audit, cancellationToken),
            cancellationToken);

    private async Task<AdminMutationResult> CreateCoreAsync(
        string name,
        string? displayName,
        string? description,
        AuditOperation audit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return await DenyMissingNameAsync(name, displayName, cancellationToken);

        if (!ScopeValidationHelper.IsValidScopeName(name))
            return await DenyInvalidNameAsync(name, displayName, cancellationToken);

        bool scopeNameIsInUse = await _configurationDbContext.ApiScopes
            .AsNoTracking()
            .AnyAsync(s => s.Name == name, cancellationToken);

        if (scopeNameIsInUse)
            return await DenyNameCollisionAsync(name, displayName, "A scope with this name already exists.",
                cancellationToken);

        bool identityResourceNameIsInUse = await _configurationDbContext.IdentityResources
            .AsNoTracking()
            .AnyAsync(r => r.Name == name, cancellationToken);

        if (identityResourceNameIsInUse)
            return await DenyNameCollisionAsync(name, displayName,
                "An identity resource with this name already exists.", cancellationToken);

        audit.TargetName = displayName ?? name;

        var newScope = new ApiScope
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            Enabled = true
        };

        _configurationDbContext.ApiScopes.Add(newScope);
        await _configurationDbContext.SaveChangesAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AdminAuditEvent(
            AuditCategory.ApiScope, AuditAction.Create, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
            name, displayName ?? name,
            Details: $"Created API Scope '{name}'"), cancellationToken);

        return AdminMutationResult.Success();

    }

    private async Task<AdminMutationResult> DenyMissingNameAsync(
        string name,
        string? displayName,
        CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.Create, AuditReasonCode.ValidationFailed, name, displayName ?? name,
            "Scope name is required.", cancellationToken);
        return AdminMutationResult.ValidationFailure(string.Empty, "Scope name is required.");
    }

    private async Task<AdminMutationResult> DenyInvalidNameAsync(
        string name,
        string? displayName,
        CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.Create, AuditReasonCode.ValidationFailed, name, displayName ?? name,
            "Scope name contains invalid characters. Spaces are not allowed.", cancellationToken);
        return AdminMutationResult.ValidationFailure(string.Empty,
            "Scope name contains invalid characters. Spaces are not allowed.");
    }

    public Task<bool> UpdateBasicsAsync(
        string name,
        string? displayName,
        string? description,
        bool enabled,
        bool required,
        bool emphasize,
        bool showInDiscoveryDocument,
        CancellationToken cancellationToken = default) =>
        ExecuteAuditedAsync(
            AuditAction.Update,
            name ?? string.Empty,
            displayName ?? name ?? string.Empty,
            audit => UpdateBasicsCoreAsync(name ?? string.Empty, displayName, description, enabled, required, emphasize,
                showInDiscoveryDocument, audit, cancellationToken),
            cancellationToken);

    private async Task<bool> UpdateBasicsCoreAsync(
        string name,
        string? displayName,
        string? description,
        bool enabled,
        bool required,
        bool emphasize,
        bool showInDiscoveryDocument,
        AuditOperation audit, CancellationToken cancellationToken = default)
    {
        ApiScope? entity = await _configurationDbContext.ApiScopes
            .FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
        if (entity is null)
            return await HandleScopeNotFoundAsync(AuditAction.Update, name, cancellationToken);

        audit.TargetName = displayName ?? name;

        entity.DisplayName = displayName;
        entity.Description = description;
        entity.Enabled = enabled;
        entity.Required = required;
        entity.Emphasize = emphasize;
        entity.ShowInDiscoveryDocument = showInDiscoveryDocument;

        await _configurationDbContext.SaveChangesAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AdminAuditEvent(
            AuditCategory.ApiScope, AuditAction.Update, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
            name, displayName ?? name,
            Details: $"Updated basic settings for API Scope '{name}'"), cancellationToken);

        return true;

    }

    public Task<bool> AddClaimAsync(ScopeName name, ClaimType claimType,
        CancellationToken cancellationToken = default) =>
        ExecuteAuditedAsync(
            AuditAction.AddClaim,
            name.Value,
            name.Value,
            audit => AddClaimCoreAsync(name.Value, claimType, audit, cancellationToken),
            cancellationToken);

    private async Task<bool> AddClaimCoreAsync(string name, ClaimType claimType,
        AuditOperation audit, CancellationToken cancellationToken = default)
    {
        if (!claimType.IsValid)
            return await DenyInvalidClaimTypeAsync(name, cancellationToken);

        ApiScope? entity = await LoadScopeAsync(name, false, cancellationToken);
        if (entity is null)
            return await HandleScopeNotFoundAsync(AuditAction.AddClaim, name, cancellationToken);

        if (entity.UserClaims.All(c => c.Type != claimType.Value))
        {
            audit.TargetName = name;
            entity.UserClaims.Add(new ApiScopeClaim { Type = claimType.Value });
            await _configurationDbContext.SaveChangesAsync(cancellationToken);

            await _auditWriter.WriteAsync(new AdminAuditEvent(
                AuditCategory.ApiScope, AuditAction.AddClaim, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
                name, name,
                Details: $"Added claim '{claimType}' to API Scope '{name}'"), cancellationToken);

        }

        return true;
    }

    private async Task<bool> DenyInvalidClaimTypeAsync(string name, CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.AddClaim, AuditReasonCode.ValidationFailed, name, name,
            "Claim type is required and must not exceed the maximum length.", cancellationToken);
        return false;
    }

    public Task<bool> RemoveClaimAsync(ScopeName name, ClaimType claimType,
        CancellationToken cancellationToken = default) =>
        ExecuteAuditedAsync(
            AuditAction.RemoveClaim,
            name.Value,
            name.Value,
            audit => RemoveClaimCoreAsync(name.Value, claimType, audit, cancellationToken),
            cancellationToken);

    private async Task<bool> RemoveClaimCoreAsync(string name, ClaimType claimType,
        AuditOperation audit, CancellationToken cancellationToken = default)
    {
        ApiScope? entity = await LoadScopeAsync(name, false, cancellationToken);
        ApiScopeClaim? claim = entity?.UserClaims.FirstOrDefault(c => c.Type == claimType.Value);
        if (entity is null || claim is null)
            return await DenyClaimNotFoundAsync(name, claimType, cancellationToken);

        audit.TargetName = name;

        entity.UserClaims.Remove(claim);
        await _configurationDbContext.SaveChangesAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AdminAuditEvent(
            AuditCategory.ApiScope, AuditAction.RemoveClaim, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
            name, name,
            Details: $"Removed claim '{claimType}' from API Scope '{name}'"), cancellationToken);

        return true;

    }

    private async Task<bool> DenyClaimNotFoundAsync(string name, ClaimType claimType,
        CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.RemoveClaim, AuditReasonCode.NotFound, name, name,
            $"API Scope '{name}' or claim '{claimType}' was not found.", cancellationToken);
        return false;
    }

    private async Task<ApiScope?> LoadScopeAsync(string name, bool asNoTracking, CancellationToken cancellationToken)
    {
        IQueryable<ApiScope> query = _configurationDbContext.ApiScopes
            .Include(s => s.UserClaims)
            .AsQueryable();

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
    }

    private static ApiScopeEditorModel MapToEditorModel(ApiScope entity)
    {
        return new ApiScopeEditorModel
        {
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Enabled = entity.Enabled,
            Required = entity.Required,
            Emphasize = entity.Emphasize,
            ShowInDiscoveryDocument = entity.ShowInDiscoveryDocument,
            Claims = entity.UserClaims.Select(c => c.Type).OrderBy(c => c).ToList()
        };
    }

    private Task AuditDeniedAsync(AuditAction action, AuditReasonCode reasonCode, string targetId, string targetName,
        string details, CancellationToken cancellationToken)
        => _auditWriter.WriteAsync(new AdminAuditEvent(
            AuditCategory.ApiScope, action, AuditOutcome.Denied, reasonCode,
            targetId, targetName, Details: details), cancellationToken);

    private Task<T> ExecuteAuditedAsync<T>(
        AuditAction action,
        string targetId,
        string targetName,
        Func<AuditOperation, Task<T>> operation,
        CancellationToken cancellationToken) =>
        AuditOperation.RunAsync(_auditWriter, AuditCategory.ApiScope, action, targetId, targetName, operation, cancellationToken);

    private async Task<AdminMutationResult> DenyNameCollisionAsync(
        string name,
        string? displayName,
        string message,
        CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(AuditAction.Create, AuditReasonCode.NameCollision, name, displayName ?? name,
            message, cancellationToken);
        return AdminMutationResult.ConflictResult(string.Empty, message);
    }

    private async Task<bool> HandleScopeNotFoundAsync(AuditAction action, string name,
        CancellationToken cancellationToken)
    {
        await AuditDeniedAsync(action, AuditReasonCode.NotFound, name, name,
            $"API Scope '{name}' was not found.", cancellationToken);
        return false;
    }
}