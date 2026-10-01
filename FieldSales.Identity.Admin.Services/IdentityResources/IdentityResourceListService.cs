using FieldSales.Identity.Services.Persistence;
using System.Data;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Scopes;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Identity.Services.IdentityResources;

public class IdentityResourceListService(
    ConfigurationDbContext configurationDbContext,
    IScopeUsageService scopeUsageService,
    IAuditWriter auditWriter) : IIdentityResourceListService
{
    private readonly IAuditWriter _auditWriter = auditWriter;
    private readonly ConfigurationDbContext _configurationDbContext = configurationDbContext;
    private readonly IScopeUsageService _scopeUsageService = scopeUsageService;

    public async Task<ListResult<IdentityResourceListItem>> GetIdentityResourcesAsync(
        ListQuery query,
        CancellationToken cancellationToken = default)
    {
        Pagination pagination = query.Pagination.Normalize();

        IQueryable<IdentityResource> dbQuery =
            ApplyFilter(_configurationDbContext.IdentityResources.AsNoTracking(), query.Filter);

        int totalCount = await dbQuery.CountAsync(cancellationToken);

        List<IdentityResourceListItem> items = await dbQuery
            .OrderBy(r => r.Name)
            .ThenBy(r => r.DisplayName)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(r => new IdentityResourceListItem
            {
                Name = r.Name,
                DisplayName = r.DisplayName,
                Description = r.Description,
                Enabled = r.Enabled,
                Required = r.Required,
                Emphasize = r.Emphasize,
                ShowInDiscoveryDocument = r.ShowInDiscoveryDocument,
                UserClaimsCount = r.UserClaims.Count,
                NonEditable = r.NonEditable,
                ClientReferenceCount = 0
            })
            .ToListAsync(cancellationToken);

        var scopeNames = ScopeSet.FromStrings(items.Select(i => i.Name));
        ScopeUsageCounts referenceCounts =
            await _scopeUsageService.GetClientReferenceCountsAsync(scopeNames, cancellationToken);

        foreach (IdentityResourceListItem item in items)
            item.ClientReferenceCount = referenceCounts[ScopeName.Create(item.Name)];

        return ListResult<IdentityResourceListItem>.Page(items, totalCount, pagination);
    }

    public async Task<IdentityResourceDeleteResult> DeleteIdentityResourceAsync(string name,
        CancellationToken cancellationToken = default)
    {
        if (BuiltInIdentityResourcePolicy.IsProtectedName(name))
            return await DenyProtectedNameBlockedAsync(name, cancellationToken);

        string targetName = name;

        return await AuditOperation.RunAsync(_auditWriter, AuditCategory.IdentityResource, AuditAction.Delete, name, targetName, async audit =>
        {
            var (outcome, deniedReason, deniedDetails) = await IdentityTransactions.RunAsync(_configurationDbContext, IsolationLevel.Serializable, async transaction =>
            {
                IdentityResource? resource = await _configurationDbContext.IdentityResources
                    .Include(r => r.UserClaims)
                    .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

                if (resource is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (Outcome: IdentityResourceDeleteResult.NotFound,
                        Reason: AuditReasonCode.NotFound, Details: (string?)$"Identity Resource '{name}' was not found.");
                }

                targetName = resource.DisplayName ?? name;
                audit.TargetName = targetName;

                if (resource.NonEditable)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (Outcome: IdentityResourceDeleteResult.Blocked,
                        Reason: AuditReasonCode.ProtectedResource, Details: (string?)$"Identity Resource '{name}' is not editable.");
                }

                ScopeUsageCounts referenceCounts = await _scopeUsageService.GetClientReferenceCountsAsync(
                    ScopeSet.FromStrings(new[] { name }), cancellationToken);
                if (referenceCounts[ScopeName.Create(name)] > 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (Outcome: IdentityResourceDeleteResult.Blocked,
                        Reason: AuditReasonCode.ReferencedResource, Details: (string?)$"Identity Resource '{name}' is referenced by one or more clients.");
                }

                _configurationDbContext.IdentityResources.Remove(resource);
                await _configurationDbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return (Outcome: IdentityResourceDeleteResult.Deleted,
                        Reason: AuditReasonCode.Succeeded, Details: (string?)null);
            }, cancellationToken);

            if (outcome != IdentityResourceDeleteResult.Deleted)
            {
                await _auditWriter.WriteAsync(new AdminAuditEvent(
                    AuditCategory.IdentityResource, AuditAction.Delete, AuditOutcome.Denied,
                    deniedReason,
                    name, targetName, Details: deniedDetails), cancellationToken);
                return outcome;
            }

            await _auditWriter.WriteAsync(new AdminAuditEvent(
                AuditCategory.IdentityResource, AuditAction.Delete, AuditOutcome.Succeeded, AuditReasonCode.Succeeded,
                name, targetName,
                Details: $"Deleted Identity Resource '{name}'"), cancellationToken);
            return IdentityResourceDeleteResult.Deleted;
        }, cancellationToken);
    }

    private async Task<IdentityResourceDeleteResult> DenyProtectedNameBlockedAsync(string name, CancellationToken cancellationToken)
    {
        await _auditWriter.WriteAsync(new AdminAuditEvent(
            AuditCategory.IdentityResource, AuditAction.Delete, AuditOutcome.Denied,
            AuditReasonCode.ProtectedResource,
            name, name, Details: $"'{name}' is a protected identity resource name."), cancellationToken);
        return IdentityResourceDeleteResult.Blocked;
    }

    private static IQueryable<IdentityResource> ApplyFilter(IQueryable<IdentityResource> query, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return query;

        string? escaped = LikeExtensions.EscapeLikePattern(filter.Trim());
        string pattern = $"%{escaped}%";

        return query.Where(r => EF.Functions.Like(r.Name, pattern) ||
                                (r.DisplayName != null && EF.Functions.Like(r.DisplayName, pattern)));
    }
}