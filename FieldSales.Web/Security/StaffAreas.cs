using System.Security.Claims;
using FieldSales.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Web.Security;

public sealed record StaffArea(string Key, string Role, string Label, string Description, string Route);

public static class StaffAreas
{
    public const string Rep = "rep";
    public const string Manager = "manager";
    public const string HeadOffice = "head-office";

    public static IReadOnlyList<StaffArea> All { get; } =
    [
        new(Rep, StaffRoles.FieldSalesperson, "Field sales", "Visit customers and manage your sales work.", "/Rep"),
        new(Manager, StaffRoles.SalesManager, "Sales manager", "Review and support your sales team.", "/Manager"),
        new(HeadOffice, StaffRoles.HeadOfficeUser, "Head office", "Access head office sales tools.", "/HeadOffice")
    ];

    public static IReadOnlyList<StaffArea> PermittedTo(ClaimsPrincipal user) =>
        All.Where(area => user.IsInRole(area.Role)).ToArray();

    public static StaffArea? Find(string? key) =>
        All.FirstOrDefault(area => string.Equals(area.Key, key, StringComparison.Ordinal));

    public static StaffArea? ForLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith('/') || url.StartsWith("//", StringComparison.Ordinal))
            return null;
        string path = url.Split(['?', '#'], 2)[0].TrimEnd('/');
        return All.FirstOrDefault(area => string.Equals(path, area.Route, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(area.Route + "/", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class StaffAreaService(IServiceProvider services, TimeProvider clock)
{
    public async Task<string?> GetLastPermittedAreaAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        string? subject = user.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return null;

        StaffWebDbContext db = services.GetRequiredService<StaffWebDbContext>();
        string? key = await db.StaffAreaPreferences.AsNoTracking()
            .Where(preference => preference.SubjectId == subject)
            .Select(preference => preference.Area)
            .SingleOrDefaultAsync(cancellationToken);
        StaffArea? area = StaffAreas.Find(key);
        return area is not null && user.IsInRole(area.Role) ? area.Route : null;
    }

    public async Task<bool> RememberAreaAsync(ClaimsPrincipal user, string key, CancellationToken cancellationToken = default)
    {
        StaffArea? area = StaffAreas.Find(key);
        string? subject = user.FindFirst("sub")?.Value;
        if (area is null || string.IsNullOrWhiteSpace(subject) || !user.IsInRole(area.Role)) return false;

        StaffWebDbContext db = services.GetRequiredService<StaffWebDbContext>();
        StaffAreaPreference? preference = await db.StaffAreaPreferences.FindAsync([subject], cancellationToken);
        if (preference is null)
        {
            preference = new StaffAreaPreference { SubjectId = subject };
            db.StaffAreaPreferences.Add(preference);
        }
        preference.Area = area.Key;
        preference.UpdatedUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
