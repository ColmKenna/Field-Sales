using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Secrets;
using FieldSales.Identity.Services.Validation;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Identity.Services.Apis;

public partial class ApiResourceEditorService : IApiResourceEditorService
{
    private readonly CategoryAudit _audit;
    private readonly ConfigurationDbContext _configurationDbContext;
    private readonly TimeProvider _timeProvider;

    public ApiResourceEditorService(
        ConfigurationDbContext configurationDbContext,
        IAuditWriter auditWriter,
        TimeProvider? timeProvider = null)
    {
        _configurationDbContext = configurationDbContext;
        _audit = AuditOperation.For(auditWriter, AuditCategory.ApiResource);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<List<string>> GetAllApiScopeNamesAsync(CancellationToken cancellationToken = default) =>
        _configurationDbContext.ApiScopes
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

    private async Task<ApiResource?> LoadResourceAsync(string name, bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<ApiResource> query = _configurationDbContext.ApiResources
            .AsSplitQuery()
            .Include(r => r.Secrets)
            .Include(r => r.Scopes)
            .Include(r => r.UserClaims)
            .AsQueryable();

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
    }

    private static ApiResourceEditorModel MapToEditorModel(ApiResource entity)
    {
        return new ApiResourceEditorModel
        {
            IsNew = false,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            Enabled = entity.Enabled,
            Secrets = entity.Secrets
                .OrderByDescending(s => s.Created)
                .Select(s => new ApiResourceSecretItem
                {
                    Id = s.Id,
                    Description = s.Description,
                    Type = s.Type.ParseSecretType(),
                    Expiration = s.Expiration,
                    Created = s.Created
                })
                .ToList(),
            Scopes = entity.Scopes.Select(s => s.Scope).OrderBy(s => s).ToList(),
            Claims = entity.UserClaims.Select(c => c.Type).OrderBy(c => c).ToList()
        };
    }

    private async Task<AdminMutationResult> DenyResourceNotFoundAsync(AuditAction action, string name, CancellationToken cancellationToken)
    {
        await _audit.DeniedAsync(action, AuditReasonCode.NotFound, name, name,
            $"API Resource '{name}' was not found.", cancellationToken);
        return AdminMutationResult.NotFoundResult();
    }
}
