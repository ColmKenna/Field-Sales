using FieldSales.StaffAccess;

namespace FieldSales.Api.Coverage;

// Who may grant or remove a rep's Restriction Permission. Any Sales Manager or Head Office User may view.
public static class RestrictionPermissionRule
{
    /// <param name="actorRoles">The actor's current business roles.</param>
    /// <param name="repManagerSubject">The rep's reporting-line manager, or null when none is recorded.</param>
    public static bool CanChange(string actorSubject, IEnumerable<string> actorRoles, string repSubject, string? repManagerSubject)
    {
        // Nobody changes their own permissions, Head Office included.
        if (string.Equals(actorSubject, repSubject, StringComparison.Ordinal)) return false;
        string[] roles = actorRoles.ToArray();
        if (roles.Contains(BusinessRoles.HeadOfficeUser, StringComparer.Ordinal)) return true;
        return roles.Contains(BusinessRoles.SalesManager, StringComparer.Ordinal)
            && string.Equals(repManagerSubject, actorSubject, StringComparison.Ordinal);
    }
}
