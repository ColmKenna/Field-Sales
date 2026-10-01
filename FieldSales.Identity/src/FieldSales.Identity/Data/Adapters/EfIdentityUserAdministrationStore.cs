using FieldSales.Identity.Presentation;
using FieldSales.Identity.Services.Persistence;
using System.Data;
using System.Security.Claims;
using FieldSales.Identity.Services;
using FieldSales.Identity.Services.AuditLogs;
using FieldSales.Identity.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FieldSales.Identity.Data.Adapters;

/// <summary>
///     EF/ASP.NET Core Identity-backed <see cref="IIdentityUserAdministrationStore" /> adapter. Owns
///     every direct dependency on <see cref="ApplicationUser" />/<see cref="ApplicationDbContext" />,
///     including the transactional protected-admin invariants (self-demotion and last-administrator
///     protection) that must be evaluated atomically alongside the role-membership check they guard.
/// </summary>
public sealed class EfIdentityUserAdministrationStore(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager, TimeProvider? timeProvider = null) : IIdentityUserAdministrationStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public async Task<ListResult<UserListItem>> GetUsersAsync(
        ListQuery query,
        CancellationToken cancellationToken = default)
    {
        Pagination pagination = query.Pagination.Normalize();

        IQueryable<ApplicationUser> dbQuery = ApplyFilter(_dbContext.Users.AsNoTracking(), query.Filter);

        int totalCount = await dbQuery.CountAsync(cancellationToken);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        List<UserListItem> items = await dbQuery
            .OrderBy(u => u.UserName)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(u => new UserListItem
            {
                Id = UserId.Create(u.Id),
                UserName = u.UserName ?? u.Id,
                Email = u.Email,
                FullName = u.FullName,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > now,
                LockoutEnd = u.LockoutEnd
            })
            .ToListAsync(cancellationToken);

        return ListResult<UserListItem>.Page(items, totalCount, pagination);
    }

    public async Task<UserUnlockOutcome> UnlockUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        string userIdStr = userId.Value ?? string.Empty;
        ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
        if (user is null) return new UserUnlockOutcome(UserUnlockResult.NotFound, userIdStr);

        string targetName = user.UserName ?? user.Id;

        IdentityResult lockoutResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!lockoutResult.Succeeded) return new UserUnlockOutcome(UserUnlockResult.Failed(lockoutResult), targetName);

        IdentityResult resetResult = await _userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded) return new UserUnlockOutcome(UserUnlockResult.Failed(resetResult), targetName);

        return new UserUnlockOutcome(UserUnlockResult.Succeeded, targetName);
    }

    public async Task<UserCreateOutcome> CreateUserAsync(
        UserCreateInputModel input, CancellationToken cancellationToken = default)
    {
        string userName = input.UserName?.Trim() ?? string.Empty;
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = input.Email.Trim(),
            FullName = string.IsNullOrWhiteSpace(input.FullName) ? null : input.FullName.Trim(),
            EmailConfirmed = true
        };

        IdentityResult result = await _userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            AuditReasonCode reasonCode =
                result.Errors.Any(e => e.Code.StartsWith("Duplicate", StringComparison.Ordinal))
                    ? AuditReasonCode.NameCollision
                    : AuditReasonCode.ValidationFailed;
            return new UserCreateOutcome(UserCreateResult.Failed(result.Errors.Select(e => e.Description).ToList()),
                reasonCode);
        }

        return new UserCreateOutcome(UserCreateResult.Succeeded(user.Id), AuditReasonCode.Succeeded);
    }

    public async Task<UserAccountDetails?> FindUserDetailsAsync(UserId userId,
        CancellationToken cancellationToken = default)
    {
        string userIdStr = userId.Value ?? string.Empty;
        ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
        if (user is null) return null;

        var assignedRoles = (await _userManager.GetRolesAsync(user)).OrderBy(r => r).ToList();
        List<string> allRoles =
            await _roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToListAsync(cancellationToken);
        var claims = (await _userManager.GetClaimsAsync(user))
            .Select(c => new UserClaim(c.Type, c.Value))
            .ToList();

        return new UserAccountDetails(
            UserId.Create(user.Id),
            user.UserName ?? user.Id,
            user.Email,
            user.FullName,
            user.LockoutEnd.HasValue && user.LockoutEnd.Value > _timeProvider.GetUtcNow(),
            user.LockoutEnd,
            assignedRoles,
            allRoles,
            claims);
    }

    public async Task<RoleAdditionOutcome> AddRoleAsync(UserId userId, string role,
        CancellationToken cancellationToken = default)
    {
        string userIdStr = userId.Value ?? string.Empty;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
            if (user is null) return new RoleAdditionOutcome(RoleAdditionStatus.UserNotFound, userIdStr, null);

            string targetName = user.UserName ?? user.Id;

            if (!await _roleManager.RoleExistsAsync(role))
            {
                return new RoleAdditionOutcome(RoleAdditionStatus.RoleNotFound, targetName, null);
            }

            if (await _userManager.IsInRoleAsync(user, role))
            {
                return new RoleAdditionOutcome(RoleAdditionStatus.AlreadyMember, targetName, null);
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            IdentityResult result = await _userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                string errorMessage = result.Describe();

                return new RoleAdditionOutcome(RoleAdditionStatus.ValidationFailed, targetName, errorMessage);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RoleAdditionOutcome(RoleAdditionStatus.Added, targetName, null);
        });
    }

    public async Task<RoleRemovalOutcome> RemoveRoleAsync(
        UserId userId,
        string role,
        string protectedRoleName,
        UserId? actingUserId,
        CancellationToken cancellationToken = default)
    {
        string userIdStr = userId.Value ?? string.Empty;
        string? actingUserIdStr = actingUserId?.Value;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RoleRemovalOutcome(RoleRemovalStatus.UserNotFound, userIdStr, false, null);
            }

            string targetName = user.UserName ?? user.Id;
            IdentityRole? roleEntity = await _roleManager.FindByNameAsync(role);
            if (roleEntity is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RoleRemovalOutcome(RoleRemovalStatus.RoleNotFound, targetName, false, null);
            }

            bool isMember = await _dbContext.UserRoles.AnyAsync(
                ur => ur.UserId == user.Id && ur.RoleId == roleEntity.Id, cancellationToken);
            if (!isMember)
            {
                await transaction.CommitAsync(cancellationToken);
                return new RoleRemovalOutcome(RoleRemovalStatus.Succeeded, targetName, false, null);
            }

            bool isProtectedRole =
                string.Equals(roleEntity.Name, protectedRoleName, StringComparison.OrdinalIgnoreCase);
            if (isProtectedRole && UserActionPolicy.IsSelf(actingUserIdStr, user.Id))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RoleRemovalOutcome(RoleRemovalStatus.SelfDemotionBlocked, targetName, false, null);
            }

            if (isProtectedRole)
            {
                int protectedMemberCount = await _dbContext.UserRoles
                    .CountAsync(ur => ur.RoleId == roleEntity.Id, cancellationToken);
                if (protectedMemberCount <= 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new RoleRemovalOutcome(RoleRemovalStatus.LastProtectedMemberBlocked, targetName, false,
                        null);
                }
            }

            IdentityResult result = await _userManager.RemoveFromRoleAsync(user, roleEntity.Name!);
            if (!result.Succeeded)
            {
                string errorMessage = result.Describe();

                await transaction.RollbackAsync(cancellationToken);
                return new RoleRemovalOutcome(RoleRemovalStatus.ValidationFailed, targetName, false, errorMessage);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RoleRemovalOutcome(RoleRemovalStatus.Succeeded, targetName, true, null);
        });
    }

    public async Task<ClaimMutationOutcome> AddClaimAsync(
        UserId userId, UserClaim claim, CancellationToken cancellationToken = default)
    {
        string claimType = claim.Type ?? string.Empty;
        string claimValue = claim.Value ?? string.Empty;
        string userIdStr = userId.Value ?? string.Empty;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
            if (user is null) return new ClaimMutationOutcome(ClaimMutationStatus.UserNotFound, userIdStr, null);

            string targetName = user.UserName ?? user.Id;

            await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            bool alreadyExists = await _dbContext.UserClaims.AnyAsync(
                claim => claim.UserId == userIdStr
                         && claim.ClaimType == claimType
                         && claim.ClaimValue == claimValue,
                cancellationToken);
            if (alreadyExists)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new ClaimMutationOutcome(ClaimMutationStatus.AlreadyExists, targetName, null);
            }

            IdentityResult result = await _userManager.AddClaimAsync(user, new Claim(claimType, claimValue));
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                string errorMessage = result.Describe();

                return new ClaimMutationOutcome(ClaimMutationStatus.ValidationFailed, targetName, errorMessage);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ClaimMutationOutcome(ClaimMutationStatus.Applied, targetName, null);
        });
    }

    public async Task<ClaimMutationOutcome> RemoveClaimAsync(
        UserId userId, UserClaim claim, CancellationToken cancellationToken = default)
    {
        string claimType = claim.Type ?? string.Empty;
        string claimValue = claim.Value ?? string.Empty;
        string userIdStr = userId.Value ?? string.Empty;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
            if (user is null) return new ClaimMutationOutcome(ClaimMutationStatus.UserNotFound, userIdStr, null);

            string targetName = user.UserName ?? user.Id;

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            IdentityResult result = await _userManager.RemoveClaimAsync(user, new Claim(claimType, claimValue));
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                string errorMessage = result.Describe();

                return new ClaimMutationOutcome(ClaimMutationStatus.ValidationFailed, targetName, errorMessage);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ClaimMutationOutcome(ClaimMutationStatus.Applied, targetName, null);
        });
    }

    public async Task<SecurityStampRotationOutcome> RotateSecurityStampAsync(
        UserActionContext context, CancellationToken cancellationToken = default)
    {
        UserId userId = context.Target;
        string userIdStr = userId.Value ?? string.Empty;
        string? actingUserIdStr = context.ActingUser?.Value;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SecurityStampRotationOutcome(SecurityStampRotationStatus.UserNotFound, userIdStr);
            }

            string targetName = user.UserName ?? user.Id;
            if (UserActionPolicy.IsSelf(actingUserIdStr, user.Id))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SecurityStampRotationOutcome(SecurityStampRotationStatus.SelfActionBlocked, targetName);
            }

            IdentityResult stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
                throw new InvalidOperationException("The user's security stamp could not be updated.");

            await transaction.CommitAsync(cancellationToken);
            return new SecurityStampRotationOutcome(SecurityStampRotationStatus.Succeeded, targetName);
        });
    }

    public async Task<PasswordResetOutcome> ResetPasswordAsync(
        UserId userId, string newPassword, CancellationToken cancellationToken = default)
    {
        string userIdStr = userId.Value ?? string.Empty;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
            if (user is null) return new PasswordResetOutcome(PasswordResetStatus.UserNotFound, userIdStr, null);

            string targetName = user.UserName ?? user.Id;

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            string token = await _userManager.GeneratePasswordResetTokenAsync(user);
            IdentityResult result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                string errorMessage = result.Describe();

                return new PasswordResetOutcome(PasswordResetStatus.ValidationFailed, targetName, errorMessage);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PasswordResetOutcome(PasswordResetStatus.Succeeded, targetName, null);
        });
    }

    public async Task<UserSuspendOutcome> SuspendUserAsync(
        UserActionContext context, CancellationToken cancellationToken = default)
    {
        UserId userId = context.Target;
        string userIdStr = userId.Value ?? string.Empty;
        string? actingUserIdStr = context.ActingUser?.Value;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
            if (user is null) return new UserSuspendOutcome(UserSuspendStatus.UserNotFound, userIdStr);

            string targetName = user.UserName ?? user.Id;

            if (UserActionPolicy.IsSelf(actingUserIdStr, user.Id))
            {
                return new UserSuspendOutcome(UserSuspendStatus.SelfActionBlocked, targetName);
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            IdentityResult result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new UserSuspendOutcome(UserSuspendStatus.ValidationFailed, targetName);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new UserSuspendOutcome(UserSuspendStatus.Succeeded, targetName);
        });
    }

    public async Task<UserDeleteOutcome> DeleteUserAsync(
        UserActionContext context, CancellationToken cancellationToken = default)
    {
        UserId userId = context.Target;
        string userIdStr = userId.Value ?? string.Empty;
        string? actingUserIdStr = context.ActingUser?.Value;
        return await IdentityTransactions.RetryAsync(_dbContext, async () =>
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(userIdStr);
            if (user is null) return new UserDeleteOutcome(UserDeleteStatus.UserNotFound, userIdStr);

            string targetName = user.UserName ?? user.Id;

            if (UserActionPolicy.IsSelf(actingUserIdStr, user.Id))
            {
                return new UserDeleteOutcome(UserDeleteStatus.SelfActionBlocked, targetName);
            }

            await using IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            IdentityResult result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new UserDeleteOutcome(UserDeleteStatus.ValidationFailed, targetName);
            }

            // Note: Since DeleteAsync doesn't always automatically persist everything depending on how UserManager is configured, 
            // ensure DB is saved.
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new UserDeleteOutcome(UserDeleteStatus.Succeeded, targetName);
        });
    }

    private static IQueryable<ApplicationUser> ApplyFilter(IQueryable<ApplicationUser> query, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return query;

        string? escaped = LikeExtensions.EscapeLikePattern(filter.Trim().ToUpperInvariant());
        string pattern = $"%{escaped}%";

        return query.Where(u =>
            (u.NormalizedUserName != null && EF.Functions.Like(u.NormalizedUserName, pattern)) ||
            (u.NormalizedEmail != null && EF.Functions.Like(u.NormalizedEmail, pattern)) ||
            (u.FullName != null && EF.Functions.Like(u.FullName.ToUpper(), pattern)));
    }
}