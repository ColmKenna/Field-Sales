using System.Security.Claims;
using FieldSales.Identity.Data;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Identity;

public static class StaffDirectoryEndpoints
{
    public static void MapStaffDirectoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/staff/directory").RequireAuthorization("StaffRoleLookup");
        group.MapGet("", async (ClaimsPrincipal actor, UserManager<ApplicationUser> users,
            ApplicationDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!await MayReadAsync(actor, users)) return Results.Forbid(authenticationSchemes: ["StaffRoleLookup"]);
            return Results.Ok(await ReadAsync(db, clock.GetUtcNow(), null, ct));
        });
        group.MapPost("/lookup", async (StaffDirectoryLookupRequest request, ClaimsPrincipal actor,
            UserManager<ApplicationUser> users, ApplicationDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!await MayReadAsync(actor, users)) return Results.Forbid(authenticationSchemes: ["StaffRoleLookup"]);
            if (request.Subjects is not { Length: > 0 and <= StaffDirectoryContract.MaximumSubjects } subjects
                || subjects.Any(subject => !StaffDirectoryContract.ValidSubject(subject)))
                return Results.BadRequest();
            return Results.Ok(await ReadAsync(db, clock.GetUtcNow(), subjects.Distinct(StringComparer.Ordinal).ToArray(), ct));
        });
    }

    private static async Task<bool> MayReadAsync(ClaimsPrincipal actor, UserManager<ApplicationUser> users)
    {
        string? subject = actor.GetStaffSubject();
        if (!StaffDirectoryContract.ValidSubject(subject)) return false;
        var user = await users.FindByIdAsync(subject!);
        if (user is null || user.Id != subject || await users.IsLockedOutAsync(user)) return false;
        var roles = await users.GetRolesAsync(user);
        return roles.Contains(BusinessRoles.SalesManager) || roles.Contains(BusinessRoles.HeadOfficeUser);
    }

    private static async Task<StaffDirectoryEntry[]> ReadAsync(ApplicationDbContext db, DateTimeOffset now,
        string[]? subjects, CancellationToken ct)
    {
        var rows = await (from user in db.Users.AsNoTracking()
            where subjects == null
                ? db.UserRoles.Any(link => link.UserId == user.Id && db.Roles.Any(role => role.Id == link.RoleId
                    && BusinessRoles.All.Contains(role.Name!)))
                : subjects.Contains(user.Id)
            join link in db.UserRoles on user.Id equals link.UserId into links
            from link in links.DefaultIfEmpty()
            join role in db.Roles on link.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new { user.Id, user.FullName, user.UserName, user.LockoutEnabled, user.LockoutEnd,
                Role = role == null ? null : role.Name }).ToArrayAsync(ct);
        // Identity may use a case-insensitive collation. Subjects on the wire remain exact.
        return rows.Where(row => subjects == null || subjects.Contains(row.Id, StringComparer.Ordinal))
            .GroupBy(row => row.Id, StringComparer.Ordinal).Select(group =>
            {
                var user = group.First();
                return new StaffDirectoryEntry(user.Id, string.IsNullOrWhiteSpace(user.FullName) ? user.UserName! : user.FullName,
                    group.Select(row => row.Role).Where(role => role is not null && BusinessRoles.Contains(role))
                        .Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    !user.LockoutEnabled || user.LockoutEnd is null || user.LockoutEnd <= now);
            }).OrderBy(entry => entry.Subject, StringComparer.Ordinal).ToArray();
    }
}
