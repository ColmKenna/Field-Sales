using FieldSales.Identity.Services.Persistence;
using FieldSales.Identity.Presentation;
using FieldSales.Identity.Services.Users;
using FieldSales.Identity.Services;
using FieldSales.Identity.Services.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FieldSales.Identity.Data.Adapters;

public sealed class EfRoleAdministrationStore(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager) : IRoleAdministrationStore
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;

    public async Task<ListResult<RoleListItem>> GetRolesAsync(
        ListQuery query,
        CancellationToken cancellationToken = default)
    {
        Pagination pagination = query.Pagination.Normalize();

        IQueryable<IdentityRole> dbQuery = ApplyFilter(_dbContext.Roles.AsNoTracking(), query.Filter);

        int totalCount = await dbQuery.CountAsync(cancellationToken);

        var rows = await dbQuery
            .OrderBy(r => r.Name)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new RoleListItem
        {
            Id = RoleId.Create(r.Id),
            Name = r.Name ?? string.Empty,
            IsProtected = r.Name == ProtectedAdminRoles.SysAdmin
        }).ToList();

        return ListResult<RoleListItem>.Page(items, totalCount, pagination);
    }

    public async Task<RoleDetailsModel?> FindRoleAsync(RoleId roleId, CancellationToken cancellationToken = default)
    {
        string roleIdStr = roleId.Value ?? string.Empty;
        IdentityRole? role = await _dbContext.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleIdStr, cancellationToken);

        if (role is null) return null;

        return new RoleDetailsModel
        {
            Id = RoleId.Create(role.Id),
            Name = role.Name ?? string.Empty,
            IsProtected = role.Name == ProtectedAdminRoles.SysAdmin
        };
    }

    public async Task<(RoleCreateOutcome Status, RoleId? RoleId, string? ErrorMessage)> CreateRoleAsync(
        RoleCreateInputModel input, CancellationToken cancellationToken = default)
    {
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            IdentityRole? existingRole = await _roleManager.FindByNameAsync(input.Name);
            if (existingRole is not null)
            {
                return (RoleCreateOutcome.NameCollision, (RoleId?)null, (string?)null);
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var newRole = new IdentityRole(input.Name);
            IdentityResult result = await _roleManager.CreateAsync(newRole);

            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                string errorMessage = result.Describe();

                return (RoleCreateOutcome.ValidationFailed, (RoleId?)null, errorMessage);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (RoleCreateOutcome.Succeeded, (RoleId?)RoleId.Create(newRole.Id), (string?)null);
        });
    }

    public async Task<(RoleDeleteOutcome Status, string TargetName)> DeleteRoleAsync(RoleId roleId,
        string protectedRoleName, CancellationToken cancellationToken = default)
    {
        string roleIdStr = roleId.Value ?? string.Empty;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            IdentityRole? role = await _roleManager.FindByIdAsync(roleIdStr);
            if (role is null) return (RoleDeleteOutcome.RoleNotFound, roleIdStr);

            string targetName = role.Name ?? role.Id;

            if (string.Equals(role.Name, protectedRoleName, StringComparison.OrdinalIgnoreCase))
            {
                return (RoleDeleteOutcome.ProtectedRoleBlocked, targetName);
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            IdentityResult result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                return (RoleDeleteOutcome.ValidationFailed, targetName);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (RoleDeleteOutcome.Succeeded, targetName);
        });
    }

    private static IQueryable<IdentityRole> ApplyFilter(IQueryable<IdentityRole> query, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return query;

        string? escaped = LikeExtensions.EscapeLikePattern(filter.Trim().ToUpperInvariant());
        string pattern = $"%{escaped}%";

        return query.Where(r => r.NormalizedName != null && EF.Functions.Like(r.NormalizedName, pattern));
    }
}