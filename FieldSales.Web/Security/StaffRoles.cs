using FieldSales.StaffAccess;

namespace FieldSales.Web.Security;

public static class StaffRoles
{
    public const string FieldSalesperson = BusinessRoles.FieldSalesperson;
    public const string SalesManager = BusinessRoles.SalesManager;
    public const string HeadOfficeUser = BusinessRoles.HeadOfficeUser;
    public static readonly string[] All = BusinessRoles.All;
}
