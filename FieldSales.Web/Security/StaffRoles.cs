namespace FieldSales.Web.Security;

public static class StaffRoles
{
    public const string FieldSalesperson = "Field Salesperson";
    public const string SalesManager = "Sales Manager";
    public const string HeadOfficeUser = "Head Office User";
    public static readonly string[] All = [FieldSalesperson, SalesManager, HeadOfficeUser];
}
